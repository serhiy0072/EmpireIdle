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

/// <summary>Джоб годує звіринець за час від минулого годування (рішення 13.10.2026).</summary>
public class PayFoodUpkeepCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public PayFoodUpkeepCommandTests()
        => _servers.GetLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);

    /// <summary>Звіринець 1 рівня їсть 100 за годину, кожен звір — ще 100 плюс 10 за рівень.</summary>
    private static GameCatalog Catalog() => new GameConfigBuilder().WithMap(m => m.Geometry = new MapGeometryConfig { RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 0.4, FogMaxShare = 1.0 }).WithBeasts(b => b.FoodUpkeep = new FoodUpkeepConfig
    {
        PerPenLevelPerHour = 100,
        PerBeastPerHour = 100,
        PerBeastLevelPerHour = 10,
        HungerDefenceMultiplier = 0.5,
        HungerProductionMultiplier = 0.5
    }).BuildCatalog();

    private PayFoodUpkeepCommandHandler Handler(GameCatalog catalog, DateTime at) => new(_villages, _pens, _servers,
        TestEffects.Resolver(_effects, _pens, catalog), new VillageCapacities(catalog), new VillageStatus(catalog),
        new WorldGeometry(catalog.Config.Map), _unitOfWork, catalog, new FakeTimeProvider(at),
        NullLogger<PayFoodUpkeepCommandHandler>.Instance);

    /// <summary>Село зі звіринцем 1 рівня й одним вовком 1 рівня: разом 210 їжі за годину.</summary>
    private Village GivenVillage(GameCatalog catalog, int food)
    {
        var (x, y) = new WorldGeometry(catalog.Config.Map).Centre;
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", [.. TestKeys.AllResources], x, y);
        village.GrantStartingResources(new Dictionary<string, int> { [TestKeys.Food] = food }, Now);
        village.AddBuilding(catalog.MainBuildingKey, catalog.Buildings, Now);
        village.AddBuilding(TestKeys.BeastPen, catalog.Buildings, Now);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);

        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(TestKeys.Beast, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        return village;
    }

    private static long Food(Village village) => village.Resources.Single(r => r.ResourceType == TestKeys.Food).Amount;

    /// <summary>Перше годування лише запускає відлік — звіринець ще нічого не їв.</summary>
    [Fact]
    public async Task Handle_ShouldOnlyStartTheClock_OnTheFirstRun()
    {
        var catalog = Catalog();
        var village = GivenVillage(catalog, food: 1000);

        await Handler(catalog, Now).Handle(new PayFoodUpkeepCommand(village.Id), CancellationToken.None);

        Assert.Equal(1000, Food(village));
        Assert.Equal(Now, village.FoodUpkeepAt);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Дві години — 420 їжі: звіринець 100 + вовк 100 + 10 за його рівень, за кожну годину.</summary>
    [Fact]
    public async Task Handle_ShouldChargeForTheHoursSinceTheLastMeal()
    {
        var catalog = Catalog();
        var village = GivenVillage(catalog, food: 1000);
        await Handler(catalog, Now).Handle(new PayFoodUpkeepCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(2)).Handle(new PayFoodUpkeepCommand(village.Id), CancellationToken.None);

        Assert.Equal(580, Food(village));
        Assert.False(village.IsHungry);
    }

    /// <summary>Їжі не вистачило — село голодує.</summary>
    [Fact]
    public async Task Handle_ShouldStarveTheVillage_WhenFoodRunsOut()
    {
        var catalog = Catalog();
        var village = GivenVillage(catalog, food: 100);
        await Handler(catalog, Now).Handle(new PayFoodUpkeepCommand(village.Id), CancellationToken.None);

        await Handler(catalog, Now.AddHours(1)).Handle(new PayFoodUpkeepCommand(village.Id), CancellationToken.None);

        Assert.Equal(0, Food(village));
        Assert.True(village.IsHungry);
    }

    /// <summary>Села вже немає — джоб іде далі без помилки.</summary>
    [Fact]
    public async Task Handle_ShouldSkip_AVanishedVillage()
    {
        await Handler(Catalog(), Now).Handle(new PayFoodUpkeepCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
