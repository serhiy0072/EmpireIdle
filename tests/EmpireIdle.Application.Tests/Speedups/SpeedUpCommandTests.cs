using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Villages.Commands;
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
/// Три хендлери прискорення ділять одну криву й один шлях списання gems,
/// тому й перевіряються однаково: ціна, зсув таймера, безкоштовний поріг.
/// </summary>
public class SpeedUpCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

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
        Monetization = new MonetizationConfig
        {
            InstantFinishThresholdMinutes = 5,
            SpeedUpFactor = 1.2,
            SpeedUpExponent = 0.75
        }
    };

    private static SpeedUpCalculator Calculator() => new(Config().Monetization);

    private PlayerWallet GivenWallet(int gems = 1000)
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "seed", PlayerId, Now);

        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);
        _currentPlayer.UserId.Returns(UserId);

        return wallet;
    }

    private Village GivenVillageWithConstruction(int minutesLeft)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 10_000 }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("farm", catalog.Buildings, Now);

        var farm = village.Buildings.Single(b => b.Type == "farm");

        farm.BeginUpgrade(catalog.Buildings["farm"], TimeSpan.FromMinutes(minutesLeft), Now,
            ProductionBoost.None, locationMultiplier: 1.0);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        return village;
    }

    private SpeedUpConstructionCommandHandler ConstructionHandler() => new(
        _villages, _wallets, _currentPlayer, _unitOfWork,
        Calculator(), new GameCatalog(Config()), new FakeTimeProvider(Now),
        NullLogger<SpeedUpConstructionCommandHandler>.Instance);

    /// <summary>
    /// Коротка черга безкоштовна: гравець не має платити за хвилину,
    /// і без порога ціна прискорення була б абсурдною для дрібниць.
    /// </summary>
    [Fact]
    public async Task SpeedUpConstruction_ShouldChargeNothing_BelowTheFreeThreshold()
    {
        var village = GivenVillageWithConstruction(minutesLeft: 3);
        var wallet = GivenWallet();
        var building = village.Buildings.Single(b => b.Type == "farm");

        await ConstructionHandler().Handle(
            new SpeedUpConstructionCommand(PlayerId, building.Id), CancellationToken.None);

        Assert.Equal(1000, wallet.GemBalance.Value);
    }

    /// <summary>Прискорення завершує будівництво одразу, не чекаючи сканера.</summary>
    [Fact]
    public async Task SpeedUpConstruction_ShouldCompleteTheUpgradeImmediately()
    {
        var village = GivenVillageWithConstruction(minutesLeft: 60);
        GivenWallet();
        var building = village.Buildings.Single(b => b.Type == "farm");

        await ConstructionHandler().Handle(
            new SpeedUpConstructionCommand(PlayerId, building.Id), CancellationToken.None);

        Assert.False(building.IsUnderConstruction);
        Assert.Equal(2, building.Level.Value);
    }

    /// <summary>Довга черга списує gems за кривою.</summary>
    [Fact]
    public async Task SpeedUpConstruction_ShouldChargeGems_AboveTheThreshold()
    {
        var village = GivenVillageWithConstruction(minutesLeft: 120);
        var wallet = GivenWallet();
        var building = village.Buildings.Single(b => b.Type == "farm");

        var expected = Calculator().GetInstantFinishCost(building.ConstructionCompletesAt!.Value, Now);

        await ConstructionHandler().Handle(
            new SpeedUpConstructionCommand(PlayerId, building.Id), CancellationToken.None);

        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(1000 - expected, wallet.GemBalance.Value);
    }

    /// <summary>Будівля не в стані будівництва — прискорювати нічого.</summary>
    [Fact]
    public async Task SpeedUpConstruction_ShouldThrow_WhenNothingIsBeingBuilt()
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);

        village.AddBuilding("townhall", catalog.Buildings, Now);
        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);

        GivenWallet();
        var idle = village.Buildings.Single();

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            ConstructionHandler().Handle(
                new SpeedUpConstructionCommand(PlayerId, idle.Id), CancellationToken.None));
    }

    /// <summary>Чужий марш не прискорити: пошук іде серед походів свого гарнізону.</summary>
    [Fact]
    public async Task SpeedUpMarch_ShouldThrow_WhenTheMarchBelongsToSomeoneElse()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([]);

        GivenWallet();

        var handler = new SpeedUpMarchCommandHandler(
            _villages, _garrisons, _marches, _wallets, _currentPlayer, _unitOfWork,
            new FakeTimeProvider(Now), Calculator(),
            NullLogger<SpeedUpMarchCommandHandler>.Instance);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            handler.Handle(new SpeedUpMarchCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>
    /// Прискорення маршу зсуває прибуття на «зараз» — бій проведе сканер,
    /// а не сам хендлер: інакше бій відбувався б у двох різних місцях коду.
    /// </summary>
    [Fact]
    public async Task SpeedUpMarch_ShouldMoveArrivalToNow()
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 0, 0);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        var march = new March(
            Guid.NewGuid(), 1, garrison.Id, 0, 0, 10, 10,
            MarchTargetType.Monster, Guid.NewGuid(),
            new Dictionary<string, int> { ["infantry"] = 5 },
            Now.AddHours(2), Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);

        GivenWallet();

        var handler = new SpeedUpMarchCommandHandler(
            _villages, _garrisons, _marches, _wallets, _currentPlayer, _unitOfWork,
            new FakeTimeProvider(Now), Calculator(),
            NullLogger<SpeedUpMarchCommandHandler>.Instance);

        await handler.Handle(new SpeedUpMarchCommand(PlayerId, march.Id), CancellationToken.None);

        Assert.Equal(Now, march.ArrivesAt);
    }
}
