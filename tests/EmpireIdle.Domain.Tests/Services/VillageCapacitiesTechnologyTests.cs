using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Технології сховищ (рішення 28.10.2026) множать недоторканий запас: склад 1 рівня
/// береже 40 їжі, +25% — уже 50, і саме стільки грабіжник не винесе.
/// </summary>
public class VillageCapacitiesTechnologyTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    private static readonly GameCatalog Catalog = new(new GameConfig
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.0 },
            new BuildingConfig
            {
                Key = "warehouse", StoresResources = ["food"], BaseStorage = 1_000, ProtectedStorage = 40, UpgradeCostGrowth = 1.0
            }
        ]
    });

    private static Village Village()
    {
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test", ["food"], 0, 0);
        village.AddBuilding("townhall", Catalog.Buildings, Now);
        village.AddBuilding("warehouse", Catalog.Buildings, Now);

        return village;
    }

    [Fact]
    public void ProtectedReserveFor_ShouldGrow_WithTheVaultTechnology()
    {
        var capacities = new VillageCapacities(Catalog);

        Assert.Equal(40, capacities.ProtectedReserveFor(Village(), "food"));
        Assert.Equal(50, capacities.ProtectedReserveFor(Village(), "food", protectedBonus: 0.25));
    }

    [Fact]
    public void Lootable_ShouldLeaveTheGrownReserve()
    {
        var village = Village();
        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 100 }, Now);
        var plunder = new PlunderCalculator(Catalog, new VillageCapacities(Catalog));

        var loot = plunder.Lootable(village, Domain.ValueObjects.ProductionBoost.None, 1.0, Now, protectedBonus: 0.25);

        Assert.Equal(50, loot["food"]);
    }
}
