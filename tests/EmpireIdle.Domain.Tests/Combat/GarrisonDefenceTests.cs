using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Combat;

/// <summary>Склад оборони (рішення 15.10.2026): найсильніший кожного класу, без лідера.</summary>
public class GarrisonDefenceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private static (Hero Hero, string Class, double Power) Candidate(string heroClass, double power, int minutesAgo = 0)
        => (new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, TestKeys.CommonHero, Guid.NewGuid(), asLeader: false, Now.AddMinutes(-minutesAgo)),
            heroClass, power);

    [Fact]
    public void Pick_ShouldTakeTheStrongestOfEachClass()
    {
        var warrior = Candidate("warrior", 500);
        var weakWarrior = Candidate("warrior", 400);
        var archer = Candidate("archer", 300);
        var knight = Candidate("knight", 350);

        var defenders = GarrisonDefence.Pick([weakWarrior, archer, warrior, knight], count: 3);

        Assert.Equal([warrior.Hero, knight.Hero, archer.Hero], defenders);
    }

    /// <summary>Двоє сильних воїнів не займуть два місця: третій клас бере свого найкращого, хай і слабшого.</summary>
    [Fact]
    public void Pick_ShouldNotTakeTwoHeroesOfOneClass()
    {
        var warrior = Candidate("warrior", 900);
        var secondWarrior = Candidate("warrior", 800);
        var archer = Candidate("archer", 100);

        var defenders = GarrisonDefence.Pick([warrior, secondWarrior, archer], count: 3);

        Assert.Equal([warrior.Hero, archer.Hero], defenders);
    }

    /// <summary>Поранений не боронить: місце бере наступний того ж класу.</summary>
    [Fact]
    public void Pick_ShouldSkipTheWounded()
    {
        var wounded = Candidate("warrior", 900);
        wounded.Hero.Wound(Now);
        var healthy = Candidate("warrior", 100);

        Assert.Equal([healthy.Hero], GarrisonDefence.Pick([wounded, healthy], count: 3));
    }

    [Fact]
    public void Pick_ShouldStopAtTheCount()
    {
        var defenders = GarrisonDefence.Pick([Candidate("warrior", 3), Candidate("knight", 2), Candidate("archer", 1)], count: 2);

        Assert.Equal(2, defenders.Count);
    }

    /// <summary>За рівної сили — давніший герой: склад не стрибає між однаковими.</summary>
    [Fact]
    public void Pick_ShouldPreferTheEarlierHero_OnATie()
    {
        var older = Candidate("warrior", 100, minutesAgo: 60);
        var newer = Candidate("warrior", 100);

        Assert.Equal([older.Hero], GarrisonDefence.Pick([newer, older], count: 3));
    }
}
