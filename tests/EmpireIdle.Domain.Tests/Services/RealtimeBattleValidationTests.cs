using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Одномоментний бій арени й вежі. Кожен тест ламає одну річ у валідному конфігу.</summary>
public class RealtimeBattleValidationTests
{
    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(h => h.RoleUnits = new()
            {
                ["warrior"] = TestKeys.Infantry,
                ["knight"] = TestKeys.Cavalry,
                ["archer"] = TestKeys.Archer,
                ["mage"] = TestKeys.Archer
            })
            .Build();

        var battle = config.RealtimeBattle;
        battle.HeroAttackIntervals = new() { ["warrior"] = 1.25, ["knight"] = 1.0, ["archer"] = 1.3, ["mage"] = 1.3 };
        battle.UnitAttackIntervals = new() { [TestKeys.Infantry] = 1.25, [TestKeys.Cavalry] = 1.0, [TestKeys.Archer] = 1.4 };
        battle.TypeAdvantages = new()
        {
            [TestKeys.Infantry] = TestKeys.Archer,
            [TestKeys.Archer] = TestKeys.Cavalry,
            [TestKeys.Cavalry] = TestKeys.Infantry
        };

        return config;
    }

    [Fact]
    public void ValidConfig_ShouldPass() => GameConfigValidator.Validate(Valid());

    /// <summary>Порожні словники — бій ще не налаштований, як RoleUnits у героях.</summary>
    [Fact]
    public void EmptyBattleSettings_ShouldPass()
    {
        var config = Valid();
        config.RealtimeBattle.HeroAttackIntervals.Clear();
        config.RealtimeBattle.UnitAttackIntervals.Clear();
        config.RealtimeBattle.TypeAdvantages.Clear();

        GameConfigValidator.Validate(config);
    }

    /// <summary>Клас без темпу удару стояв би в бою й не бив.</summary>
    [Fact]
    public void HeroClassWithoutInterval_ShouldFail()
    {
        var config = Valid();
        config.RealtimeBattle.HeroAttackIntervals.Remove("mage");

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Конвої ведуть лише ті типи, що в RoleUnits, — кожен із них має бити.</summary>
    [Fact]
    public void LedUnitWithoutInterval_ShouldFail()
    {
        var config = Valid();
        config.RealtimeBattle.UnitAttackIntervals.Remove(TestKeys.Cavalry);

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Конвой без здоров'я падав би з першого удару.</summary>
    [Fact]
    public void LedUnitWithoutHealth_ShouldFail()
    {
        var config = Valid();
        config.Units.Single(u => u.Key == TestKeys.Archer).Stats.Remove("Health");

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Юніт, якого не веде жоден герой, у бій не виходить — здоров'я йому не потрібне.</summary>
    [Fact]
    public void UnledUnitWithoutHealth_ShouldPass()
    {
        var config = Valid();
        config.HeroSettings.RoleUnits["knight"] = TestKeys.Infantry;
        config.RealtimeBattle.UnitAttackIntervals.Remove(TestKeys.Cavalry);
        config.Units.Single(u => u.Key == TestKeys.Cavalry).Stats.Remove("Health");

        GameConfigValidator.Validate(config);
    }

    [Fact]
    public void AdvantageOverUnknownUnit_ShouldFail()
    {
        var config = Valid();
        config.RealtimeBattle.TypeAdvantages[TestKeys.Infantry] = "dragon";

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    /// <summary>Перевага над собою множила б дзеркальний бій лише одній стороні.</summary>
    [Fact]
    public void AdvantageOverItself_ShouldFail()
    {
        var config = Valid();
        config.RealtimeBattle.TypeAdvantages[TestKeys.Infantry] = TestKeys.Infantry;

        Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }
}
