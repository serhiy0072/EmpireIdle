using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Вміння конкретного героя (GDD §6.1, рішення 08.10.2026): рівень героя відкриває,
/// зірки стелять рівень вміння, небойові вміння пришвидшують марш.
/// </summary>
public class HeroSkillsTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    private static readonly HeroesConfig Settings = new() { MaxSkillLevel = 6, PartsPerStar = 6, SkillBookCosts = [3, 5, 7, 10, 12] };

    /// <summary>Книги за рівень (рішення 15.10.2026): 3, 5, 7, 10, 12; без списку — одна книга.</summary>
    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(5, 12)]
    [InlineData(9, 12)]
    public void BookCost_ShouldFollowTheTable(int currentLevel, int expected)
        => Assert.Equal(expected, new HeroSkills(Settings).BookCost(currentLevel));

    [Fact]
    public void BookCost_ShouldBeOneBook_WithoutATable()
        => Assert.Equal(1, new HeroSkills(new HeroesConfig { MaxSkillLevel = 6 }).BookCost(3));

    private static HeroSkills Skills() => new(Settings);

    private static HeroSkillConfig Swift(int unlockLevel = 1) => new()
    {
        Key = "swift", DisplayName = "Swift", Half = SkillHalf.Defense, Kind = SkillKind.Utility, UnlockLevel = unlockLevel,
        Utility = new SkillUtilityConfig { Effect = SkillUtilityConfig.MarchSpeed, Percents = [5, 10, 15, 20, 25, 30] },
    };

    private static HeroConfig HeroWith(params HeroSkillConfig[] skills) => new()
    {
        Key = TestKeys.CommonHero, DisplayName = "Hero", Class = "warrior", Skills = [.. skills],
    };

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, true)]
    public void IsUnlocked_ShouldFollowTheHeroLevel(int heroLevel, bool expected)
        => Assert.Equal(expected, HeroSkills.IsUnlocked(TestKit.Entities.Hero(level: heroLevel), Swift(unlockLevel: 20)));

    [Fact]
    public void LevelOf_ShouldBeZero_WhileTheSkillIsLocked()
        => Assert.Equal(0, Skills().LevelOf(TestKit.Entities.Hero(level: 1), Swift(unlockLevel: 20)));

    [Fact]
    public void LevelOf_ShouldStartAtOne_OnceUnlocked()
        => Assert.Equal(1, Skills().LevelOf(TestKit.Entities.Hero(level: 20), Swift(unlockLevel: 20)));

    /// <summary>Зірки + 1, не вище за стелю: без зірок — перший рівень, на п'ятій зірці — шостий.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(4, 5)]
    [InlineData(5, 6)]
    public void LevelCap_ShouldBeStarsPlusOne(int stars, int expected)
        => Assert.Equal(expected, Skills().LevelCap(TestKit.Entities.Hero(stars: stars)));

    [Fact]
    public void LevelCap_ShouldNotExceedTheMaxSkillLevel()
    {
        var skills = new HeroSkills(new HeroesConfig { MaxSkillLevel = 3, PartsPerStar = 6 });

        Assert.Equal(3, skills.LevelCap(TestKit.Entities.Hero(stars: 5)));
    }

    /// <summary>Рівень понад довжину списку бере останнє значення: вміння не обнуляється від правки стелі.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 5)]
    [InlineData(3, 15)]
    [InlineData(9, 30)]
    public void At_ShouldReadThePerLevelValue(int level, double expected)
        => Assert.Equal(expected, HeroSkills.At([5, 10, 15, 20, 25, 30], level));

    [Fact]
    public void UtilityPercent_ShouldSumTheUnlockedSkillsOfTheEffect()
    {
        var config = HeroWith(Swift(), Swift(unlockLevel: 40));

        Assert.Equal(5, Skills().UtilityPercent(TestKit.Entities.Hero(level: 20), config, SkillUtilityConfig.MarchSpeed));
        Assert.Equal(10, Skills().UtilityPercent(TestKit.Entities.Hero(level: 40), config, SkillUtilityConfig.MarchSpeed));
    }

    [Fact]
    public void UtilityPercent_ShouldBeZero_ForAnUnknownHero()
        => Assert.Equal(0, Skills().UtilityPercent(TestKit.Entities.Hero(), null, SkillUtilityConfig.MarchSpeed));

    /// <summary>Небойове вміння пришвидшує всю колону: +5% — множник 1.05.</summary>
    [Fact]
    public void MarchSpeedMultiplier_ShouldApplyTheUtilitySkill()
    {
        var progression = new HeroProgression(Settings);

        Assert.Equal(1.05, progression.MarchSpeedMultiplier(TestKit.Entities.Hero(), HeroWith(Swift())), 6);
        Assert.Equal(1.0, progression.MarchSpeedMultiplier(TestKit.Entities.Hero(), HeroWith()), 6);
    }

    private static Domain.Combat.HeroGear SpeedGear(params (Domain.Entities.Hero Hero, double Percent)[] heroes)
        => new(heroes.ToDictionary(h => h.Hero.Id,
            h => (IReadOnlyDictionary<string, double>)new Dictionary<string, double> { [Domain.Combat.HeroGear.MarchSpeed] = h.Percent }));

    /// <summary>Спорядження героїв маршу складається з небойовими вміннями: 5% вміння + 10% + 8% спорядження.</summary>
    [Fact]
    public void MarchSpeedMultiplier_ShouldAddTheGearOfEveryHero()
    {
        var swift = TestKit.Entities.Hero();
        var plain = TestKit.Entities.Hero();

        var multiplier = new HeroProgression(Settings)
            .MarchSpeedMultiplier([(swift, HeroWith(Swift())), (plain, HeroWith())], SpeedGear((swift, 10), (plain, 8)));

        Assert.Equal(1.23, multiplier, 6);
    }

    /// <summary>Частка спорядження впирається в стелю, а вміння — ні: 5% + min(30, 20 + 20).</summary>
    [Fact]
    public void MarchSpeedMultiplier_ShouldCapOnlyTheGearShare()
    {
        var first = TestKit.Entities.Hero();
        var second = TestKit.Entities.Hero();
        var settings = new HeroesConfig { MaxSkillLevel = 6, PartsPerStar = 6, MaxGearMarchSpeedPercent = 30 };

        var multiplier = new HeroProgression(settings)
            .MarchSpeedMultiplier([(first, HeroWith(Swift())), (second, HeroWith())], SpeedGear((first, 20), (second, 20)));

        Assert.Equal(1.35, multiplier, 6);
    }

    // ---------- Рівні від книг ----------

    [Fact]
    public void LevelOf_ShouldFollowTheBooks_OnceUnlocked()
    {
        var hero = TestKit.Entities.Hero(level: 20, stars: 2);
        hero.RaiseSkill("swift", levelCap: 3, maxSkillLevel: 6, Now);

        Assert.Equal(2, Skills().LevelOf(hero, Swift(unlockLevel: 20)));
    }

    /// <summary>Скидання рівня героя закриває вміння, але не губить піднятий книгами рівень.</summary>
    [Fact]
    public void LevelOf_ShouldKeepTheBookLevel_ThroughALevelReset()
    {
        var hero = TestKit.Entities.Hero(level: 20, stars: 2);
        hero.RaiseSkill("swift", levelCap: 3, maxSkillLevel: 6, Now);

        hero.ResetLevel(Now);
        Assert.Equal(0, Skills().LevelOf(hero, Swift(unlockLevel: 20)));

        hero.GainLevels(19, 20, Now);
        Assert.Equal(2, Skills().LevelOf(hero, Swift(unlockLevel: 20)));
    }

    [Fact]
    public void BookFor_ShouldMatchTheClassRarityAndHalf()
    {
        var book = new SkillBookConfig { ItemKey = "book", Class = "warrior", Rarity = Rarity.Common, Half = SkillHalf.Defense };
        var skills = new HeroSkills(new HeroesConfig { SkillBooks = [book] });
        var hero = HeroWith();

        Assert.Same(book, skills.BookFor(hero, SkillHalf.Defense));
        Assert.Null(skills.BookFor(hero, SkillHalf.Attack));
        Assert.Null(skills.BookFor(new HeroConfig { Key = "x", DisplayName = "x", Class = "warrior", Rank = Rarity.Rare }, SkillHalf.Defense));
    }
}
