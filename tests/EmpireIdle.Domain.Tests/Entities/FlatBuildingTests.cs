using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Функціональні будівлі без рівнів (GDD §3.1): ринок, кузня, зала героїв…
/// Ратуша їх відкриває, і вони назавжди стоять на рівні 1.
/// </summary>
public class FlatBuildingTests
{
    private const string Market = "market";
    private const int LevelsPerTier = 10;
    private const int UngatedServerLevel = 99;

    private static Dictionary<string, BuildingConfig> Configs()
    {
        var configs = TestKit.Entities.FarmConfigs();

        configs[Market] = new BuildingConfig { Key = Market, DisplayName = "Ринок", Upgradable = false };

        return configs;
    }

    [Fact]
    public void BeginBuildingUpgrade_ShouldRefuse_ABuildingWithoutLevels()
    {
        var village = TestKit.Entities.VillageWithTownhall(townhallLevel: 5, resourceAmount: 1000);
        village.AddBuilding(Market, Configs(), TestKit.Entities.Now);
        var market = village.Buildings.Single(b => b.Type == Market);
        var before = village.Resources.ToDictionary(r => r.ResourceType, r => r.Amount);

        var refusal = Assert.Throws<InvalidStateException>(() =>
            village.BeginBuildingUpgrade(market.Id, Configs(), TestKit.Entities.Now, ProductionBoost.None,
                mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier,
                locationMultiplier: 1.0));

        Assert.Equal(RefusalReasons.BuildingNotUpgradable.Key, refusal.Reason);
        Assert.Equal("Ринок", refusal.Args["building"]);
        Assert.Equal(1, market.Level.Value);
        Assert.False(market.IsUnderConstruction);
        Assert.Equal(before, village.Resources.ToDictionary(r => r.ResourceType, r => r.Amount));
    }

    /// <summary>
    /// Правило B тірного гейта будівлі без рівнів не рахує: вона назавжди на 1
    /// й інакше зупинила б ратушу на першій же межі тіру.
    /// </summary>
    [Fact]
    public void TierGate_ShouldIgnore_BuildingsWithoutLevels()
    {
        var configs = Configs();
        var village = TestKit.Entities.VillageWithTownhall(townhallLevel: LevelsPerTier, resourceAmount: 100_000);
        village.AddBuilding(Market, configs, TestKit.Entities.Now);

        var farm = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Farm);
        TestKit.Entities.RaiseLevel(farm, configs[TestKit.TestKeys.Farm], LevelsPerTier - 1, TestKit.Entities.Now);

        var townhall = village.Buildings.Single(b => b.Type == TestKit.TestKeys.Townhall);

        village.BeginBuildingUpgrade(townhall.Id, configs, TestKit.Entities.Now, ProductionBoost.None,
            mainBuildingKey: TestKit.TestKeys.Townhall, serverLevel: UngatedServerLevel, levelsPerTier: LevelsPerTier,
            locationMultiplier: 1.0);

        Assert.True(townhall.IsUnderConstruction);
    }
}
