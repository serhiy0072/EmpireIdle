using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Combat;

/// <summary>
/// Бойові технології в бонусі стека (рішення 28.10.2026): діють без героя, на всі конвої,
/// і складаються з вміннями героя в один пул, а не множаться на них.
/// </summary>
public class StackBuffTechnologyTests
{
    private static TechnologyBonuses Bonuses(params (TechnologyStat Stat, string? Target, double Value)[] values)
    {
        var bonuses = new TechnologyBonuses();

        foreach (var (stat, target, value) in values)
            bonuses.Add(stat, target, value);

        return bonuses;
    }

    [Fact]
    public void With_ShouldStrengthenAnArmyWithoutAHero()
    {
        var buff = StackBuff.None.With(Bonuses((TechnologyStat.Attack, TestKeys.Infantry, 0.05)));

        Assert.Equal(1.05, buff.Attack(TestKeys.Infantry), 6);
        Assert.Equal(1.0, buff.Attack(TestKeys.Archer), 6);
        Assert.Equal(1.0, buff.Defense(TestKeys.Infantry), 6);
    }

    /// <summary>Бонус «для всіх» діє на кожен тип, а бонус типу додається до нього.</summary>
    [Fact]
    public void With_ShouldAddTheAllUnitsBonusToEveryType()
    {
        var buff = StackBuff.None.With(Bonuses(
            (TechnologyStat.Defense, null, 0.02),
            (TechnologyStat.Defense, TestKeys.Archer, 0.03)));

        Assert.Equal(1.02, buff.Defense(TestKeys.Infantry), 6);
        Assert.Equal(1.05, buff.Defense(TestKeys.Archer), 6);
    }

    /// <summary>Вміння героя +10% і технологія +5% дають +15%, а не ×1.1×1.05.</summary>
    [Fact]
    public void With_ShouldAddToTheHeroBonus_NotMultiplyIt()
    {
        var hero = StackBuff.None.With(Bonuses((TechnologyStat.Attack, TestKeys.Infantry, 0.10)));

        var buff = hero.With(Bonuses((TechnologyStat.Attack, TestKeys.Infantry, 0.05)));

        Assert.Equal(1.15, buff.Attack(TestKeys.Infantry), 6);
    }

    [Fact]
    public void With_ShouldIgnoreNonBattleStats()
    {
        var buff = StackBuff.None.With(Bonuses((TechnologyStat.Production, null, 0.5)));

        Assert.Same(StackBuff.None, buff);
    }

    [Fact]
    public void With_ShouldNotChangeTheOriginalBuff()
    {
        var hero = StackBuff.None.With(Bonuses((TechnologyStat.Attack, null, 0.10)));

        _ = hero.With(Bonuses((TechnologyStat.Attack, null, 0.05)));

        Assert.Equal(1.10, hero.Attack(TestKeys.Infantry), 6);
    }
}
