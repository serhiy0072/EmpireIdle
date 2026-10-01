using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Склад і нагорода монстра посилаються на юніти й ресурси каталогу; вміння героя
/// мусить уміщатись у шкалу енергії бійця данжу.
/// </summary>
public class MonsterAndAbilityValidationTests
{
    private static GameConfig WithMonster(List<UnitStack> units, List<ResourceCost> rewards)
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Resources = [new ResourceConfig { Key = "food" }];
        config.Units = [new UnitConfig { Key = "infantry" }, new UnitConfig { Key = "archer" }];
        config.Monsters =
        [
            new MonsterConfig { Key = "wolves", MinLevel = 1, MaxLevel = 5, Units = units, Rewards = rewards }
        ];

        return config;
    }

    [Fact]
    public void KnownUnitsAndResources_ShouldPass()
        => GameConfigValidator.Validate(WithMonster(
            [new UnitStack { UnitType = "infantry", Count = 10 }, new UnitStack { UnitType = "archer", Count = 5 }],
            [new ResourceCost { Resource = "food", Amount = 100 }]));

    [Fact]
    public void DuplicateUnit_ShouldFail()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithMonster(
            [new UnitStack { UnitType = "infantry", Count = 10 }, new UnitStack { UnitType = "infantry", Count = 5 }],
            [])));

    [Fact]
    public void UnknownUnit_ShouldFail()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithMonster(
            [new UnitStack { UnitType = "dragon", Count = 1 }], [])));

    [Fact]
    public void UnknownRewardResource_ShouldFail()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithMonster(
            [new UnitStack { UnitType = "infantry", Count = 10 }],
            [new ResourceCost { Resource = "mithril", Amount = 10 }])));

    [Fact]
    public void NonPositiveRewardAmount_ShouldFail()
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithMonster(
            [new UnitStack { UnitType = "infantry", Count = 10 }],
            [new ResourceCost { Resource = "food", Amount = 0 }])));

    private static GameConfig WithAbilityCost(int energyCost)
    {
        var config = new GameConfigBuilder().WithBuildings().WithHeroes().Build();

        config.Heroes.First().Abilities.Add(new HeroAbilityConfig
        {
            Key = "test_strike", DisplayName = "Strike", EnergyCost = energyCost, DamageMultiplier = 1.5
        });

        return config;
    }

    [Fact]
    public void AbilityAtFullEnergy_ShouldPass()
        => GameConfigValidator.Validate(WithAbilityCost(new DungeonsConfig().MaxEnergy));

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void AbilityOutsideTheEnergyScale_ShouldFail(int energyCost)
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithAbilityCost(energyCost)));
}
