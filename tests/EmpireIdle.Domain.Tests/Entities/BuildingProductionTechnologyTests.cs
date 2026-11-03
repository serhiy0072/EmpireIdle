using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Технології виробітку (рішення 28.10.2026): ставка × (1 + «для всіх» + «саме цей ресурс»),
/// а буст крамниці множить уже підняту ставку. Ферма 1 рівня дає 10 їжі за хвилину.
/// </summary>
public class BuildingProductionTechnologyTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    private static readonly GameCatalog Catalog = new(new GameConfig
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.0 },
            new BuildingConfig
            {
                Key = "farm", ProducesResource = "food", BaseProductionPerMinute = 10, BaseStorage = 1_000_000,
                UpgradeCostGrowth = 1.0
            },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 1_000_000, UpgradeCostGrowth = 1.0 }
        ]
    });

    private static Building Farm()
    {
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test", ["food"], 0, 0);
        village.AddBuilding("townhall", Catalog.Buildings, Now);
        village.AddBuilding("farm", Catalog.Buildings, Now);

        return village.Buildings.Single(b => b.Type == "farm");
    }

    private static ProductionBoost Technology(double forAll, double food) => ProductionBoost.None with
    {
        TechnologyForAll = forAll,
        TechnologyByResource = new Dictionary<string, double> { ["food"] = food }
    };

    [Fact]
    public void StoredAt_ShouldRaiseTheRate_ByTheAllAndTheResourceTechnologies()
    {
        // 60 хв × 10/хв × (1 + 0.1 + 0.2)
        Assert.Equal(780, Farm().StoredAt(Catalog.Buildings["farm"], Now.AddHours(1), Technology(0.1, 0.2), 1.0));
    }

    [Fact]
    public void StoredAt_ShouldIgnoreTheTechnologyOfAnotherResource()
    {
        var boost = ProductionBoost.None with { TechnologyByResource = new Dictionary<string, double> { ["wood"] = 0.5 } };

        Assert.Equal(600, Farm().StoredAt(Catalog.Buildings["farm"], Now.AddHours(1), boost, 1.0));
    }

    /// <summary>Буст ×2 на першу годину множить ставку з технологіями: 600 × 1.2 × 2 + 600 × 1.2.</summary>
    [Fact]
    public void StoredAt_ShouldMultiplyTheShopBoost_OnTopOfTheTechnologies()
    {
        var boost = new ProductionBoost(2.0, Now, Now.AddHours(1)) with { TechnologyForAll = 0.2 };

        Assert.Equal(1440 + 720, Farm().StoredAt(Catalog.Buildings["farm"], Now.AddHours(2), boost, 1.0));
    }

    /// <summary>Нове вікно буста не губить технологій: With копіює весь запис.</summary>
    [Fact]
    public void With_ShouldKeepTheTechnologies()
    {
        var boost = Technology(0.1, 0.2).With(new BoostWindow(0.15, Now, Now.AddHours(1)));

        Assert.Equal(1.3, boost.TechnologyMultiplier("food"), 10);
    }
}
