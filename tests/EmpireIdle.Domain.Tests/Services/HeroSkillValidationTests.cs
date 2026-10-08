using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Вміння героїв (GDD §6.1, рішення 08.10.2026): кожен вид має свої частини, списки — по значенню
/// на рівень, а склад за рідкістю — як у SkillLayouts. Описка тут мовчки знеструмила б героя.
/// </summary>
public class HeroSkillValidationTests
{
    private static readonly List<double> Six = [1, 2, 3, 4, 5, 6];

    private static HeroSkillConfig Active(Action<SkillBattleConfig>? tune = null, SkillHalf half = SkillHalf.Attack, int unlockLevel = 1)
    {
        var battle = new SkillBattleConfig
        {
            Target = AbilityTarget.SingleEnemy, Cooldown = 3, DamageMultiplier = 1.5, LevelScale = [.. Six],
        };
        tune?.Invoke(battle);

        return new HeroSkillConfig
        {
            Key = "smash", DisplayName = "Smash", Half = half, Kind = SkillKind.Active, UnlockLevel = unlockLevel,
            Troops = Troops(), Battle = battle,
        };
    }

    private static SkillTroopBonusConfig Troops(string target = "all", string stat = "Attack") => new()
    {
        Target = target, Stat = stat, Percents = [.. Six],
    };

    private static HeroSkillConfig Passive(string key, SkillHalf half) => new()
    {
        Key = key, DisplayName = key, Half = half, Kind = SkillKind.Passive,
        Troops = Troops(stat: half == SkillHalf.Attack ? "Attack" : "Defense"),
    };

    private static HeroSkillConfig Utility(string effect = SkillUtilityConfig.MarchSpeed, SkillHalf half = SkillHalf.Defense) => new()
    {
        Key = "swift", DisplayName = "Swift", Half = half, Kind = SkillKind.Utility,
        Utility = new SkillUtilityConfig { Effect = effect, Percents = [.. Six] },
    };

    private static GameConfig With(params HeroSkillConfig[] skills) => WithLayout(null, skills);

    /// <summary>Звичайний герой із заданими вміннями; layout — склад для звичайних, null — склад не описано.</summary>
    private static GameConfig WithLayout(SkillLayoutConfig? layout, params HeroSkillConfig[] skills)
    {
        var config = new GameConfigBuilder().WithBuildings().WithUnits().WithHeroes().Build();

        config.Heroes.First(h => h.Key == TestKeys.CommonHero).Skills = [.. skills];

        // Склад перевіряється для кожного героя рідкості — інших звичайних прибираємо, щоб не заважали
        if (layout is not null)
        {
            config.HeroSettings.SkillLayouts[nameof(Rarity.Common)] = layout;
            config.Heroes.RemoveAll(h => h.Rank == Rarity.Common && h.Key != TestKeys.CommonHero);
        }

        return config;
    }

    private static void Fails(GameConfig config)
        => Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

    [Fact]
    public void AWellFormedActive_ShouldPass()
        => GameConfigValidator.Validate(With(Active()));

    // ---------- Частини за видом ----------

    [Fact]
    public void AnActiveWithoutABattleEffect_ShouldFail()
        => Fails(With(Tweak(Active(), s => s.Battle = null)));

    [Fact]
    public void AnActiveWithoutATroopBonus_ShouldFail()
        => Fails(With(Tweak(Active(), s => s.Troops = null)));

    [Fact]
    public void APassiveWithABattleEffect_ShouldFail()
        => Fails(With(Tweak(Passive("guard", SkillHalf.Defense), s => s.Battle = Active().Battle)));

    [Fact]
    public void AUtilityWithATroopBonus_ShouldFail()
        => Fails(With(Tweak(Utility(), s => s.Troops = Troops())));

    // ---------- Половини й відкриття ----------

    /// <summary>Активне — завжди атака й з першого рівня: інакше новий герой у данжі без вміння.</summary>
    [Fact]
    public void AnActiveInTheDefenseHalf_ShouldFail()
        => Fails(With(Active(half: SkillHalf.Defense)));

    [Fact]
    public void AnActiveUnlockedLater_ShouldFail()
        => Fails(With(Active(unlockLevel: 20)));

    [Fact]
    public void AUtilityInTheAttackHalf_ShouldFail()
        => Fails(With(Utility(half: SkillHalf.Attack)));

    [Theory]
    [InlineData(0)]
    [InlineData(81)]
    public void AnUnlockLevelOutsideTheHeroLevels_ShouldFail(int level)
        => Fails(With(Tweak(Passive("guard", SkillHalf.Defense), s => s.UnlockLevel = level)));

    [Fact]
    public void TwoActives_ShouldFail()
        => Fails(With(Active(), Tweak(Active(), s => s.Key = "second")));

    // ---------- Значення ----------

    [Fact]
    public void AnUnknownUtilityEffect_ShouldFail()
        => Fails(With(Utility("GatherSpeed")));

    [Fact]
    public void AnUnknownTroopTarget_ShouldFail()
        => Fails(With(Tweak(Passive("guard", SkillHalf.Defense), s => s.Troops = Troops(target: "dragons"))));

    [Fact]
    public void AnUnknownTroopStat_ShouldFail()
        => Fails(With(Tweak(Passive("guard", SkillHalf.Defense), s => s.Troops = Troops(stat: "Speed"))));

    /// <summary>По значенню на кожен рівень вміння: короткий список обрізав би прокачку.</summary>
    [Fact]
    public void AShortLevelList_ShouldFail()
        => Fails(With(Active(b => b.LevelScale = [1, 1.2])));

    [Fact]
    public void AZeroLevelScale_ShouldFail()
        => Fails(With(Active(b => b.LevelScale = [0, 1, 1, 1, 1, 1])));

    [Fact]
    public void ACooldownBelowOneTurn_ShouldFail()
        => Fails(With(Active(b => b.Cooldown = 0)));

    [Fact]
    public void ABattleEffectThatDoesNothing_ShouldFail()
        => Fails(With(Active(b => b.DamageMultiplier = 0)));

    /// <summary>
    /// Щит без тривалості згасає в тому ж ході: стан тікає наприкінці ходу носія,
    /// тож щит на себе з однією тривалістю не поглинув би нічого.
    /// </summary>
    [Theory]
    [InlineData(AbilityTarget.Self, 2, true)]
    [InlineData(AbilityTarget.SingleAlly, 1, true)]
    [InlineData(AbilityTarget.Self, 1, false)]
    [InlineData(AbilityTarget.SingleAlly, 0, false)]
    public void AShield_ShouldOutliveItsOwnTurn(AbilityTarget target, int turns, bool valid)
    {
        var config = With(Active(b =>
        {
            b.Target = target;
            b.DamageMultiplier = 0;
            b.ShieldPercent = 0.25;
            b.ShieldTurns = turns;
        }));

        if (valid)
            GameConfigValidator.Validate(config);
        else
            Fails(config);
    }

    // ---------- Склад за рідкістю ----------

    private static readonly SkillLayoutConfig CommonLayout = new() { Attack = 2, Defense = 2, Utility = 1 };

    [Fact]
    public void TheLayoutOfTheRarity_ShouldPass()
        => GameConfigValidator.Validate(WithLayout(CommonLayout,
            Active(), Passive("fury", SkillHalf.Attack), Utility(), Passive("guard", SkillHalf.Defense)));

    [Fact]
    public void AMissingSkill_ShouldFail()
        => Fails(WithLayout(CommonLayout, Active(), Passive("fury", SkillHalf.Attack), Utility()));

    /// <summary>Без небойового звичайний герой не має логістики, якої від нього чекає гравець.</summary>
    [Fact]
    public void ACombatSkillInPlaceOfTheUtility_ShouldFail()
        => Fails(WithLayout(CommonLayout,
            Active(), Passive("fury", SkillHalf.Attack), Passive("guard", SkillHalf.Defense), Passive("wall", SkillHalf.Defense)));

    [Fact]
    public void ALayoutWithoutTheActive_ShouldFail()
        => Fails(WithLayout(CommonLayout,
            Passive("fury", SkillHalf.Attack), Passive("rage", SkillHalf.Attack), Utility(), Passive("guard", SkillHalf.Defense)));

    [Fact]
    public void AnUnknownRarityInTheLayouts_ShouldFail()
    {
        var config = With(Active());
        config.HeroSettings.SkillLayouts["Legendary"] = CommonLayout;

        Fails(config);
    }

    [Fact]
    public void MoreUtilitiesThanTheDefenseHalfHolds_ShouldFail()
        => Fails(WithLayout(new SkillLayoutConfig { Attack = 1, Defense = 1, Utility = 2 }, Active()));

    private static HeroSkillConfig Tweak(HeroSkillConfig skill, Action<HeroSkillConfig> tune)
    {
        tune(skill);
        return skill;
    }
}
