using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Щит у вмінні мусить мати тривалість: стан тікає наприкінці ходу носія, тож щит на себе
/// з однією тривалістю згас би в тому ж ході, не поглинувши нічого.
/// </summary>
public class ShieldAbilityValidationTests
{
    private static GameConfig WithShield(AbilityTarget target, int turns)
    {
        var config = new GameConfigBuilder().WithBuildings().WithHeroes().Build();

        config.Heroes.First().Abilities.Add(new HeroAbilityConfig
        {
            Key = "test_guard", DisplayName = "Guard", EnergyCost = 50, Target = target,
            ShieldPercent = 0.25, ShieldTurns = turns,
        });

        return config;
    }

    [Theory]
    [InlineData(AbilityTarget.Self, 2)]
    [InlineData(AbilityTarget.SingleAlly, 1)]
    public void LongEnoughShield_ShouldPass(AbilityTarget target, int turns)
        => GameConfigValidator.Validate(WithShield(target, turns));

    [Theory]
    [InlineData(AbilityTarget.Self, 1)]
    [InlineData(AbilityTarget.SingleAlly, 0)]
    public void ShieldThatVanishesAtOnce_ShouldFail(AbilityTarget target, int turns)
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(WithShield(target, turns)));
}
