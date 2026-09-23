using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Villages.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Villages;

/// <summary>
/// Клієнт показує вартість прискорення на кнопці ще до кліку — рахунок
/// має приїхати разом зі станом села, а не окремим запитом на кожну будівлю.
/// </summary>
public class GetVillageQueryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45,
                BaseBuildMinutes = 5, BuildTimeGrowth = 1.5,
                Cost = [new ResourceCost { Resource = "food", Amount = 100 }] },
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
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 100_000,
                UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "hospital", RequiresMainBuildingLevel = 5, UpgradeCostGrowth = 1.45 }
        ],
        Resources =
        [
            new ResourceConfig { Key = "gold", RequiresMainBuildingLevel = 0 },
            new ResourceConfig { Key = "food", RequiresMainBuildingLevel = 3 }
        ],
        Monetization = new MonetizationConfig
        {
            InstantFinishThresholdMinutes = 5,
            SpeedUpFactor = 1.2,
            SpeedUpExponent = 0.75
        }
    };

    private static GameCatalog Catalog() => new(Config());
    private static SpeedUpCalculator Calculator() => new(Config().Monetization);

    private GetVillageQueryHandler Handler() => new(
        _villages, _servers, new EffectResolver(_effects), Catalog(), new FakeTimeProvider(Now),
        new WorldGeometry(new MapConfig { Width = 1, Height = 1, MaxServerLevel = 1,
            Geometry = new MapGeometryConfig { RingBoundaries = [], RingMultipliers = [1.0],
                RingsAtFirstLevel = 1.0, FogMinShare = 0, FogMaxShare = 1.0 } }),
        new VillageStatus(Catalog()), Calculator());

    private Village GivenVillage()
    {
        var catalog = Catalog();
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food", "gold"], 0, 0);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 10_000, ["gold"] = 500 }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("farm", catalog.Buildings, Now);
        village.AddBuilding("hospital", catalog.Buildings, Now);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _servers.GetLevelAsync(village.ServerId, Arg.Any<CancellationToken>()).Returns(1);

        return village;
    }

    /// <summary>Будівля не будується — прискорювати нічого, ціна не рахується.</summary>
    [Fact]
    public async Task Handle_ShouldNotPriceABuilding_ThatIsNotUnderConstruction()
    {
        var village = GivenVillage();

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var farm = response.Buildings.Single(b => b.Type == "farm");
        Assert.Null(farm.SpeedUpCostGems);
    }

    /// <summary>Довга черга під будівництвом отримує ту саму ціну, що й SpeedUpCalculator.</summary>
    [Fact]
    public async Task Handle_ShouldPriceABuilding_UnderConstruction()
    {
        var village = GivenVillage();
        var farm = village.Buildings.Single(b => b.Type == "farm");

        farm.BeginUpgrade(Catalog().Buildings["farm"], TimeSpan.FromMinutes(120), Now, ProductionBoost.None, locationMultiplier: 1.0);

        var expected = Calculator().GetInstantFinishCost(farm.ConstructionCompletesAt!.Value, Now);

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var farmView = response.Buildings.Single(b => b.Type == "farm");
        Assert.True(expected > 0, "120 хвилин мають коштувати gems, інакше тест нічого не перевіряє.");
        Assert.Equal(expected, farmView.SpeedUpCostGems);
    }

    /// <summary>Черга коротша за безкоштовний поріг — ціна нульова, а не null.</summary>
    [Fact]
    public async Task Handle_ShouldPriceZero_BelowTheFreeThreshold()
    {
        var village = GivenVillage();
        var farm = village.Buildings.Single(b => b.Type == "farm");

        farm.BeginUpgrade(Catalog().Buildings["farm"], TimeSpan.FromMinutes(3), Now, ProductionBoost.None, locationMultiplier: 1.0);

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var farmView = response.Buildings.Single(b => b.Type == "farm");
        Assert.Equal(0, farmView.SpeedUpCostGems);
    }

    /// <summary>
    /// Туман війни (GDD §3.1): будівля існує з дня 1, але лишається закритою,
    /// поки ратуша не досягне порогу. Гейт — за IsUnlocked, не за відсутністю будівлі.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldMarkAGatedBuilding_AsLocked_BelowTheRequiredMainBuildingLevel()
    {
        GivenVillage();

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var hospital = response.Buildings.Single(b => b.Type == "hospital");
        Assert.False(hospital.IsUnlocked);
    }

    /// <summary>Ратуша без гейта відкрита одразу — інакше й ферма зникла б з екрана.</summary>
    [Fact]
    public async Task Handle_ShouldMarkAnUngatedBuilding_AsUnlocked()
    {
        GivenVillage();

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var farm = response.Buildings.Single(b => b.Type == "farm");
        Assert.True(farm.IsUnlocked);
    }

    /// <summary>Той самий туман для ресурсів: food недоступний, поки ратуша не 3 рівня.</summary>
    [Fact]
    public async Task Handle_ShouldMarkAGatedResource_AsLocked_BelowTheRequiredMainBuildingLevel()
    {
        GivenVillage();

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var food = response.Resources.Single(r => r.ResourceType == "food");
        Assert.False(food.IsUnlocked);
    }

    /// <summary>Ресурс без гейта (gold) видно з дня 1.</summary>
    [Fact]
    public async Task Handle_ShouldMarkAnUngatedResource_AsUnlocked()
    {
        GivenVillage();

        var response = await Handler().Handle(new GetVillageQuery(PlayerId), CancellationToken.None);

        var gold = response.Resources.Single(r => r.ResourceType == "gold");
        Assert.True(gold.IsUnlocked);
    }
}
