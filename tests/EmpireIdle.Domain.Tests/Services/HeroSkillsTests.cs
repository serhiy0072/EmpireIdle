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

    private static readonly HeroesConfig Settings = new() { MaxSkillLevel = 6, PartsPerStar = 6 };

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
