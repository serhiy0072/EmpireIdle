using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Garrisons;

public class LevelUpUnitsCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        MaxLevelUpBatchSize = 10,
        MaxUnitLevel = 10,
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true },
            new BuildingConfig { Key = "barracks", Cost = [new ResourceCost { Resource = "food", Amount = 10 }] },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"] }
        ],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                RequiresBuilding = "barracks",
                BaseTrainMinutes = 2,
                LevelUpCostGrowth = 1.35,
                Cost = [new ResourceCost { Resource = "food", Amount = 10 }]
            }
        ]
    };

    private LevelUpUnitsCommandHandler Handler() => new(
        _villages, _garrisons, _unitOfWork, new FakeTimeProvider(Now),
        NullLogger<LevelUpUnitsCommandHandler>.Instance, new GameCatalog(Config()));

    /// <summary>Село з казармами, гарнізон із партією юнітів першого рівня, ресурси.</summary>
    private (Village Village, Garrison Garrison) GivenVillage(int infantryAtLevelOne = 10, int food = 10_000)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("barracks", catalog.Buildings, Now);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        garrison.TrainUnits("infantry", 1, infantryAtLevelOne, 100, 1000, TimeSpan.Zero, Now);
        garrison.CompleteDueTraining(Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return (village, garrison);
    }

    /// <summary>Гарнізон із партією юнітів, готовою одразу на заданому рівні (без витрат часу на це в тесті).</summary>
    private (Village Village, Garrison Garrison) GivenVillageWithStackAtLevel(int level, int count = 10, int food = 100_000)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("barracks", catalog.Buildings, Now);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        garrison.TrainUnits("infantry", level, count, 100, 1000, TimeSpan.Zero, Now);
        garrison.CompleteDueTraining(Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);

        return (village, garrison);
    }

    /// <summary>
    /// Прокачка з 3 на 4 коштує часом лише один крок (3→4), а не весь шлях
    /// з рівня 1 (§5.2 GDD) — те, що юніт уже пройшов, повторно не рахується.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeTimeOnlyForTheLevelsCrossed()
    {
        var (_, garrison) = GivenVillageWithStackAtLevel(level: 3, count: 10);

        await Handler().Handle(new LevelUpUnitsCommand(PlayerId, "infantry", 3, 4, 1), CancellationToken.None);

        // Крок(3) = 2 × 1.35² = 3.645 → 3 (усічення), для 1 юніта
        var order = Assert.Single(garrison.LevelUpOrders);
        Assert.Equal(Now.AddMinutes(3), order.CompletesAt);
    }

    /// <summary>Вартість списується за кількість юнітів у партії, за кроком рівня.</summary>
    [Fact]
    public async Task Handle_ShouldChargeCostPerUnit()
    {
        var (village, _) = GivenVillage(food: 1000);

        await Handler().Handle(new LevelUpUnitsCommand(PlayerId, "infantry", 1, 2, 4), CancellationToken.None);

        // Крок 1→2: cost(level=1) = 10 × 1.35^0 = 10, × 4 юніти = 40
        Assert.Equal(960, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Партія знімається з гарнізону й стає в чергу прокачки.</summary>
    [Fact]
    public async Task Handle_ShouldQueueLevelUpOrder_AndRemoveUnitsFromTheStack()
    {
        var (_, garrison) = GivenVillage(infantryAtLevelOne: 10);

        await Handler().Handle(new LevelUpUnitsCommand(PlayerId, "infantry", 1, 2, 4), CancellationToken.None);

        var order = Assert.Single(garrison.LevelUpOrders);
        Assert.Equal(1, order.FromLevel);
        Assert.Equal(2, order.ToLevel);
        Assert.Equal(4, order.Count);
        Assert.Equal(6, garrison.Units.Single(u => u.Level == 1).Count);
    }

    /// <summary>Не можна прокачати вище за встановлений кап (10, §5.2 GDD).</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTargetLevelExceedsTheCap()
    {
        GivenVillage();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new LevelUpUnitsCommand(PlayerId, "infantry", 1, 11, 1), CancellationToken.None));
    }

    /// <summary>Невідомий тип юніта — 404, а не 500.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_ForUnknownUnitType()
    {
        GivenVillage();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new LevelUpUnitsCommand(PlayerId, "dragon", 1, 2, 1), CancellationToken.None));
    }

    /// <summary>Нестача ресурсів зупиняє операцію до постановки в чергу.</summary>
    [Fact]
    public async Task Handle_ShouldNotQueue_WhenResourcesAreInsufficient()
    {
        var (_, garrison) = GivenVillage(food: 1);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(new LevelUpUnitsCommand(PlayerId, "infantry", 1, 2, 4), CancellationToken.None));

        Assert.Empty(garrison.LevelUpOrders);
    }
}
