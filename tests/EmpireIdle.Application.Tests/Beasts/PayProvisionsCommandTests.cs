using EmpireIdle.Application.Beasts.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Beasts;

/// <summary>
/// Джоб сплачує провізію за час від минулої оплати (рішення 13.10.2026, 20.10.2026): Їдальня їсть м'ясо,
/// звіринець — свій ресурс. Голод села й голод звірів — окремі.
/// </summary>
public class PayProvisionsCommandTests
{
    private const string MessHall = "messhall";

    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public PayProvisionsCommandTests()
        => _servers.GetLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);

    /// <summary>
    /// Звіринець 1 рівня їсть 100 за годину, кожен звір — ще 100 плюс 10 за рівень. Звірі в фікстурах
    /// платять їжею. З Їдальнею село їсть 10 за кожен рівень будівель із рівнями.
    /// </summary>
    private static GameCatalog Catalog(bool messHall = false)
    {
        // Склад — щоб ферма Їдальні мала куди класти м'ясо
        var config = new GameConfigBuilder()
            .WithBuildings(TestKeys.Warehouse)
            .WithMap(m => m.Geometry = new MapGeometryConfig { RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 0.4, FogMaxShare = 1.0 })
            .WithBeasts(b => b.FodderUpkeep = new BeastUpkeepConfig { PerPenLevelPerHour = 100, PerBeastPerHour = 100, PerBeastLevelPerHour = 10 })
            .Build();

        config.Provisions = new ProvisionsConfig
        {
            MealResource = TestKeys.Food,
            MealPerBuildingLevelPerHour = 10,
            HungerDefenceMultiplier = 0.5,
            HungerProductionMultiplier = 0.5
        };

        if (messHall)
        {
            config.Buildings.Add(new BuildingConfig { Key = MessHall, DisplayName = "Їдальня", Upgradable = false });
            // Їдальня їсть те, що хтось виробляє; у селі ферми немає — розмір не змінює
            config.Buildings.Add(new BuildingConfig
            {
                Key = "farm", DisplayName = "Ферма", ProducesResource = TestKeys.Food, BaseProductionPerMinute = 1,
                BaseStorage = 100, BaseBuildMinutes = 1, BuildTimeGrowth = 1.1, UpgradeCostGrowth = 1.1
            });
            config.Provisions.MessHallBuildingKey = MessHall;
        }

        return new GameCatalog(config);
    }

    private PayProvisionsCommandHandler Handler(GameCatalog catalog, DateTime at) => new(_villages, _pens, _servers,
        TestEffects.Resolver(_effects, _pens, catalog), new VillageCapacities(catalog), new VillageStatus(catalog),
        new WorldGeometry(catalog.Config.Map), _unitOfWork, catalog, new FakeTimeProvider(at),
        NullLogger<PayProvisionsCommandHandler>.Instance);

    /// <summary>Село з ратушею, звіринцем 1 рівня й одним вовком 1 рівня: звірі їдять 210 за годину.</summary>
    private Village GivenVillage(GameCatalog catalog, int food)
    {
        var (x, y) = new WorldGeometry(catalog.Config.Map).Centre;
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [.. TestKeys.AllResources], x, y);
        village.GrantStartingResources(new Dictionary<string, int> { [TestKeys.Food] = food }, Now);
        village.AddBuilding(catalog.MainBuildingKey, catalog.Buildings, Now);
        village.AddBuilding(TestKeys.BeastPen, catalog.Buildings, Now);
        if (catalog.Buildings.ContainsKey(MessHall))
            village.AddBuilding(MessHall, catalog.Buildings, Now);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);

        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        return village;
    }

    private static long Food(Village village) => village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    /// <summary>Перша оплата лише запускає відлік — ще ніхто нічого не їв.</summary>
    [Fact]
    public async Task Handle_ShouldOnlyStartTheClock_OnTheFirstRun()
    {
        var catalog = Catalog(messHall: true);
        var village = GivenVillage(catalog, food: 1000);

        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.Equal(1000, Food(village));
        Assert.Equal(Now, village.FoodUpkeepAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Дві години без Їдальні — лише звірі: 420 (звіринець 100 + вовк 100 + 10 за його рівень, за кожну годину).</summary>
    [Fact]
    public async Task Handle_ShouldChargeTheBeasts_ForTheHoursSinceTheLastMeal()
    {
        var catalog = Catalog();
        var village = GivenVillage(catalog, food: 1000);
        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(2)).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.Equal(580, Food(village));
        Assert.False(village.BeastsHungry);
    }

    /// <summary>
    /// Їдальня їсть за розмір селища: ратуша 1 + звіринець 1 — два рівні, 20 за годину; будівля без рівнів не рахується.
    /// Разом зі звірами за годину — 230.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldFeedTheVillage_ByItsSize()
    {
        var catalog = Catalog(messHall: true);
        var village = GivenVillage(catalog, food: 1000);
        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(1)).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.Equal(770, Food(village));
        Assert.False(village.IsHungry);
    }

    /// <summary>Село сите першим: м'яса вистачило людям, а звірам — ні. Голодні лише звірі.</summary>
    [Fact]
    public async Task Handle_ShouldStarveOnlyTheBeasts_WhenTheirShareRunsOut()
    {
        var catalog = Catalog(messHall: true);
        var village = GivenVillage(catalog, food: 100);
        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(1)).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.Equal(0, Food(village));
        Assert.False(village.IsHungry);
        Assert.True(village.BeastsHungry);
    }

    /// <summary>М'яса не вистачило навіть Їдальні — голодує село.</summary>
    [Fact]
    public async Task Handle_ShouldStarveTheVillage_WhenTheMessHallRunsOut()
    {
        var catalog = Catalog(messHall: true);
        var village = GivenVillage(catalog, food: 10);
        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(1)).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.True(village.IsHungry);
    }

    /// <summary>
    /// Джоб іде кожні 10 хвилин: за годину шість прогонів списують рівно годинну норму (230),
    /// а не 6 × floor(230 / 6) = 228 — дріб переходить на наступний прогін.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldChargeTheFullHour_InTenMinuteRuns()
    {
        var catalog = Catalog(messHall: true);
        var village = GivenVillage(catalog, food: 1000);
        await Handler(catalog, Now).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        for (var run = 1; run <= 6; run++)
            await Handler(catalog, Now.AddMinutes(run * 10)).Handle(new PayProvisionsCommand(village.Id), CancellationToken.None);

        Assert.Equal(770, Food(village));
    }

    /// <summary>Села вже немає — джоб іде далі без помилки.</summary>
    [Fact]
    public async Task Handle_ShouldSkip_AVanishedVillage()
    {
        await Handler(Catalog(), Now).Handle(new PayProvisionsCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
