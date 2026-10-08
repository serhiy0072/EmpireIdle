using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Speedups.Commands;
using EmpireIdle.Application.Speedups.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Speedups;

/// <summary>
/// Прискорення предметами (рішення 08.10.2026): хвилини кількох предметів складаються, межа
/// таймера та сама, що для gems, надлишок згорає, а на межі предмети лишаються в рюкзаку.
/// </summary>
public class SpeedUpWithItemsCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Village _village;
    private readonly Garrison _garrison;

    public SpeedUpWithItemsCommandTests()
    {
        var catalog = new GameCatalog(Config());

        _village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        _village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 10_000 }, Now);
        _village.AddBuilding("townhall", catalog.Buildings, Now);
        _village.AddBuilding("farm", catalog.Buildings, Now);
        _garrison = new Garrison(Guid.NewGuid(), _village.Id, 1);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_village);
        _garrisons.GetByVillageIdAsync(_village.Id, Arg.Any<CancellationToken>()).Returns(_garrison);
    }

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig
            {
                Key = "farm",
                ProducesResource = "food",
                BaseProductionPerMinute = 10,
                BaseStorage = 600,
                BaseBuildMinutes = 5,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45,
                Cost = [new ResourceCost { Resource = "food", Amount = 10 }]
            },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], UpgradeCostGrowth = 1.45 }
        ],
        Units = [new UnitConfig { Key = "infantry", Cost = [new ResourceCost { Resource = "food", Amount = 40 }] }],
        Items =
        [
            new ItemConfig { Key = "speedup_5m", Type = "speedup", SpeedUpMinutes = 5 },
            new ItemConfig { Key = "speedup_1h", Type = "speedup", SpeedUpMinutes = 60 },
            new ItemConfig { Key = "hero_xp_small", Type = "heroxp", HeroExperience = 500 }
        ],
        Monetization = new MonetizationConfig
        {
            SpeedUpFloorSeconds = new Dictionary<SpeedUpTimer, int>
            {
                [SpeedUpTimer.Construction] = 0,
                [SpeedUpTimer.Training] = 0,
                [SpeedUpTimer.March] = 30
            },
            SpeedUpFactor = 1.2,
            SpeedUpExponent = 0.75
        }
    };

    private SpeedUpWithItemsCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);

        return new SpeedUpWithItemsCommandHandler(_inventory,
            new SpeedUpTargets(_villages, _garrisons, _marches, catalog),
            new SpeedUpCalculator(config.Monetization), catalog, _unitOfWork, new FakeTimeProvider(Now),
            NullLogger<SpeedUpWithItemsCommandHandler>.Instance);
    }

    private PlayerItem GivenItems(string itemKey, int count)
    {
        var stack = new PlayerItem(Guid.NewGuid(), PlayerId, itemKey, count);
        _inventory.GetItemAsync(PlayerId, itemKey, Arg.Any<CancellationToken>()).Returns(stack);

        return stack;
    }

    private Building GivenConstruction(int minutesLeft)
    {
        var catalog = new GameCatalog(Config());
        var farm = _village.Buildings.Single(b => b.Type == "farm");

        farm.BeginUpgrade(catalog.Buildings["farm"], TimeSpan.FromMinutes(minutesLeft), Now,
            ProductionBoost.None, locationMultiplier: 1.0);

        return farm;
    }

    private Task Use(SpeedUpTimer timer, Guid targetId, params (string Key, int Count)[] items)
        => Handler().Handle(new SpeedUpWithItemsCommand(PlayerId, timer, targetId,
            items.ToDictionary(i => i.Key, i => i.Count)), CancellationToken.None);

    /// <summary>Хвилини кількох предметів складаються, і з таймера зрізається саме їхня сума.</summary>
    [Fact]
    public async Task Handle_ShouldCutTheSumOfAllItems()
    {
        var farm = GivenConstruction(minutesLeft: 120);
        var fives = GivenItems("speedup_5m", 3);
        var hours = GivenItems("speedup_1h", 1);

        await Use(SpeedUpTimer.Construction, farm.Id, ("speedup_5m", 2), ("speedup_1h", 1));

        Assert.Equal(Now.AddMinutes(120 - 70), farm.ConstructionCompletesAt);
        Assert.Equal(1, fives.Count);
        Assert.Equal(0, hours.Count);
        _inventory.Received(1).RemoveItem(hours);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Хвилин більше, ніж лишилось, а межа будівництва нульова: будівля добудована одразу.</summary>
    [Fact]
    public async Task Handle_ShouldFinishTheConstruction_WhenItemsCoverTheRest()
    {
        var farm = GivenConstruction(minutesLeft: 30);
        GivenItems("speedup_1h", 1);

        await Use(SpeedUpTimer.Construction, farm.Id, ("speedup_1h", 1));

        Assert.False(farm.IsUnderConstruction);
        Assert.Equal(2, farm.Level.Value);
    }

    /// <summary>Тренування з нульовою межею: партія одразу в гарнізоні.</summary>
    [Fact]
    public async Task Handle_ShouldFinishTheTraining_WhenItemsCoverTheRest()
    {
        _garrison.TrainUnits("infantry", level: 1, count: 5, maxBatchSize: 100, armyCapacity: 1000,
            TimeSpan.FromMinutes(4), Now);
        GivenItems("speedup_5m", 1);

        await Use(SpeedUpTimer.Training, _garrison.TrainingOrders.Single().Id, ("speedup_5m", 1));

        Assert.Empty(_garrison.TrainingOrders);
        Assert.Equal(5, _garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Марш зрізається лише до своєї межі в 30 с: бій лишається подією на мапі.</summary>
    [Fact]
    public async Task Handle_ShouldStopAMarchAtItsFloor()
    {
        var march = new March(Guid.NewGuid(), 1, _garrison.Id, 0, 0, 5, 5, MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 1 }, Now.AddMinutes(20), Now);
        _marches.GetActiveByGarrisonAsync(_garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);
        GivenItems("speedup_1h", 1);

        await Use(SpeedUpTimer.March, march.Id, ("speedup_1h", 1));

        Assert.Equal(Now.AddSeconds(30), march.ArrivesAt);
    }

    /// <summary>На межі прискорювати нічого: відмова, і предмети лишаються в рюкзаку.</summary>
    [Fact]
    public async Task Handle_ShouldKeepTheItems_WhenNothingIsLeftToCut()
    {
        var march = new March(Guid.NewGuid(), 1, _garrison.Id, 0, 0, 5, 5, MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 1 }, Now.AddSeconds(20), Now);
        _marches.GetActiveByGarrisonAsync(_garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);
        var stack = GivenItems("speedup_5m", 2);

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Use(SpeedUpTimer.March, march.Id, ("speedup_5m", 1)));

        Assert.Equal(RefusalReasons.SpeedUpAtFloor.Key, refusal.Reason);
        Assert.Equal(2, stack.Count);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Не прискорення (баночка досвіду) і нестача штук — відмова до будь-яких змін.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_ForANonSpeedUpOrTooFewItems()
    {
        var farm = GivenConstruction(minutesLeft: 120);
        GivenItems("hero_xp_small", 5);
        GivenItems("speedup_5m", 1);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Use(SpeedUpTimer.Construction, farm.Id, ("hero_xp_small", 1)));
        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Use(SpeedUpTimer.Construction, farm.Id, ("speedup_5m", 2)));

        Assert.Equal(Now.AddMinutes(120), farm.ConstructionCompletesAt);
    }
}
