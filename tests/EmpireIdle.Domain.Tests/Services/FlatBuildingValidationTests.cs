using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Будівля без рівнів (GDD §3.1) не може мати нічого, що росте з рівнем, і ніщо
/// не може вимагати її рівня вище 1. Кожен тест ламає одну річ у валідному конфігу.
/// </summary>
public class FlatBuildingValidationTests
{
    private const string Forge = "forge";

    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder().WithBuildings(Forge).WithUnits().Build();

        var forge = config.Buildings.Single(b => b.Key == Forge);
        forge.Upgradable = false;
        forge.Cost = [];

        return config;
    }

    private static BuildingConfig ForgeOf(GameConfig config) => config.Buildings.Single(b => b.Key == Forge);

    [Fact]
    public void ValidConfig_ShouldPass() => GameConfigValidator.Validate(Valid());

    /// <summary>Ціна апгрейду в будівлі без рівнів — ціна того, чого не купиш.</summary>
    [Fact]
    public void UpgradeCost_ShouldFail()
    {
        var config = Valid();
        ForgeOf(config).Cost = [new ResourceCost { Resource = TestKeys.Wood, Amount = 10 }];

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void LevelledEffect_ShouldFail()
    {
        var config = Valid();
        ForgeOf(config).WoundedCapacityPerLevel = 20;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Виробнича будівля без рівнів не росла б ніколи — це не функціональна будівля.</summary>
    [Fact]
    public void Production_ShouldFail()
    {
        var config = Valid();
        ForgeOf(config).ProducesResource = TestKeys.Food;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void MainBuildingWithoutLevels_ShouldFail()
    {
        var config = Valid();
        config.Buildings.Single(b => b.IsMainBuilding).Upgradable = false;
        config.Buildings.Single(b => b.IsMainBuilding).Cost = [];

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Юніт, що чекає 2 рівня кузні, недосяжний: кузня назавжди на 1.</summary>
    [Fact]
    public void UnitGatedAboveLevelOne_ShouldFail()
    {
        var config = Valid();
        var unit = config.Units.First();
        unit.RequiresBuilding = Forge;
        unit.RequiresBuildingLevel = 2;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void QuestOnUpgradingIt_ShouldFail()
    {
        var config = Valid();
        config.Quests.Add(new QuestConfig
        {
            Key = "upgrade_forge",
            Objectives = [new QuestObjectiveConfig { Type = "BuildingUpgradeCompleted", Target = Forge, Count = 2 }]
        });

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }
}
