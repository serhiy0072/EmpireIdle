using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Навчальний табір (GDD §6.1, рішення 07.10.2026): рівень дає найслабший з опорної п'ятірки
/// поза табором, герой у слоті воює з більшим із власного й табірного рівня.
/// </summary>
public class TrainingCampTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    private static readonly TrainingCampConfig Config = new()
    {
        ReferenceSize = 5,
        FreeSlotTownHallLevels = [1, 1, 5],
        ExtraSlotPricesGems = [100, 200],
        SlotCooldownHours = 24,
        SkipCooldownGems = 50,
    };

    private static TrainingCampRules Rules() => new(Config);

    private static List<Hero> Roster(params int[] levels)
        => levels.Select(level => TestKit.Entities.Hero(level: level)).ToList();

    // ---------- Рівень табору ----------

    /// <summary>Чотири прокачані й один першого рівня — табір дає перший рівень.</summary>
    [Fact]
    public void CampLevel_ShouldBeTheWeakestOfTheTopFive()
    {
        Assert.Equal(1, Rules().CampLevel(Roster(40, 40, 40, 40, 1)));
        Assert.Equal(30, Rules().CampLevel(Roster(50, 45, 40, 35, 30, 2, 1)));
    }

    [Fact]
    public void CampLevel_ShouldBeUnavailable_WithFewerThanFiveHeroesOutside()
        => Assert.Null(Rules().CampLevel(Roster(40, 40, 40, 40)));

    /// <summary>Герої в таборі в п'ятірку не входять — інакше табір підтягував би сам себе.</summary>
    [Fact]
    public void CampLevel_ShouldIgnoreHeroesInTheCamp()
    {
        var roster = Roster(60, 50, 40, 30, 20, 10);
        roster[0].EnterCamp(0, campLevel: 20, Now);

        Assert.Equal(10, Rules().CampLevel(roster));
    }

    // ---------- Слоти ----------

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    public void FreeSlots_ShouldOpenWithTheTownHall(int townHall, int expected)
        => Assert.Equal(expected, Rules().FreeSlots(townHall));

    [Fact]
    public void NextSlotPrice_ShouldWalkThePriceList_AndStopAtTheEnd()
    {
        Assert.Equal(100, Rules().NextSlotPrice(0));
        Assert.Equal(200, Rules().NextSlotPrice(1));
        Assert.Null(Rules().NextSlotPrice(2));
    }

    [Fact]
    public void FirstFreeSlot_ShouldSkipTakenAndCoolingSlots()
    {
        var camp = new TrainingCamp(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        camp.StartCooldown(1, TimeSpan.FromHours(24), Now);

        Assert.Equal(2, camp.FirstFreeSlot(totalSlots: 3, occupied: new HashSet<int> { 0 }, Now));
        Assert.Null(camp.FirstFreeSlot(totalSlots: 2, occupied: new HashSet<int> { 0 }, Now));
    }

    [Fact]
    public void Cooldown_ShouldEndOnItsOwn()
    {
        var camp = new TrainingCamp(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        camp.StartCooldown(0, TimeSpan.FromHours(24), Now);

        Assert.True(camp.IsCoolingDown(0, Now.AddHours(23)));
        Assert.False(camp.IsCoolingDown(0, Now.AddHours(24)));
    }

    [Fact]
    public void SkipCooldown_ShouldRefuse_AReadySlot()
    {
        var camp = new TrainingCamp(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => camp.SkipCooldown(0, Now));

        Assert.Equal(RefusalReasons.CampSlotReady.Key, refusal.Reason);
    }

    [Fact]
    public void AddPurchasedSlot_ShouldStopAtTheCap()
    {
        var camp = new TrainingCamp(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        camp.AddPurchasedSlot(maxPurchasable: 1, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => camp.AddPurchasedSlot(maxPurchasable: 1, Now));

        Assert.Equal(RefusalReasons.CampAllSlotsBought.Key, refusal.Reason);
        Assert.Equal(1, camp.PurchasedSlots);
    }

    // ---------- Герой у таборі ----------

    [Fact]
    public void EffectiveLevel_ShouldTakeTheCampLevel_ForAWeakerHero()
    {
        var hero = TestKit.Entities.Hero(level: 3);
        hero.EnterCamp(0, campLevel: 30, Now);

        Assert.Equal(30, hero.EffectiveLevel);
        Assert.Equal(3, hero.Level);
    }

    /// <summary>Табір ніколи не знижує: рівень табору впав нижче власного — діє власний.</summary>
    [Fact]
    public void EffectiveLevel_ShouldKeepTheOwnLevel_WhenTheCampFallsBelowIt()
    {
        var hero = TestKit.Entities.Hero(level: 40);
        hero.EnterCamp(0, campLevel: 50, Now);

        hero.SyncCampLevel(10, Now);

        Assert.Equal(40, hero.EffectiveLevel);
    }

    [Fact]
    public void LeaveCamp_ShouldReturnTheOwnLevel_AndTheFreedSlot()
    {
        var hero = TestKit.Entities.Hero(level: 3);
        hero.EnterCamp(2, campLevel: 30, Now);

        var slot = hero.LeaveCamp(Now);

        Assert.Equal(2, slot);
        Assert.Null(hero.CampSlot);
        Assert.Equal(3, hero.EffectiveLevel);
    }

    [Fact]
    public void EnterCamp_ShouldRefuse_AHeroAlreadyThere()
    {
        var hero = TestKit.Entities.Hero();
        hero.EnterCamp(0, campLevel: 10, Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => hero.EnterCamp(1, campLevel: 10, Now));

        Assert.Equal(RefusalReasons.CampHeroAlreadyIn.Key, refusal.Reason);
    }

    [Fact]
    public void LeaveCamp_ShouldRefuse_AHeroOutside()
    {
        var refusal = Assert.Throws<RequirementNotMetException>(() => TestKit.Entities.Hero().LeaveCamp(Now));

        Assert.Equal(RefusalReasons.CampHeroNotIn.Key, refusal.Reason);
    }

    /// <summary>Табірний рівень — і для статів, і для відкриття вмінь.</summary>
    [Fact]
    public void CampLevel_ShouldUnlockSkills()
    {
        var skill = new HeroSkillConfig { Key = "late", DisplayName = "Late", UnlockLevel = 40 };
        var hero = TestKit.Entities.Hero(level: 1);

        Assert.False(HeroSkills.IsUnlocked(hero, skill));

        hero.EnterCamp(0, campLevel: 40, Now);

        Assert.True(HeroSkills.IsUnlocked(hero, skill));
    }
}
