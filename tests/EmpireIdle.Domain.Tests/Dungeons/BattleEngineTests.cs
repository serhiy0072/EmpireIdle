using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Dungeons;

/// <summary>
/// Покроковий бій данжів: черга ходів, лінії, перезарядки вмінь, стани.
/// Кожен тест ламає рівно одне правило — інакше падіння не показує, яке саме.
/// </summary>
public class BattleEngineTests
{
    private static DungeonsConfig Config() => new()
    {
        DefenseSoftening = 120,
        MinDamageShare = 0.1,
        BaseCritChance = 0,
        CritMultiplier = 1.6,
    };

    private static BattleEngine Engine(DungeonsConfig? config = null) => new(config ?? Config());

    private static Combatant Hero(int index, BattleLine line = BattleLine.Front, double speed = 10,
        double health = 500, double attack = 100, Dictionary<DungeonStat, double>? unique = null,
        params CombatSkill[] skills) => new()
    {
        Index = index,
        Side = BattleSide.Heroes,
        Line = line,
        Key = $"hero{index}",
        HeroId = Guid.NewGuid(),
        Attack = attack,
        Defense = 30,
        MaxHealth = health,
        Health = health,
        Speed = speed,
        Statuses = [],
        UniqueStats = unique ?? [],
        Skills = [.. skills],
    };

    private static Combatant Enemy(int index, BattleLine line = BattleLine.Front, double speed = 5,
        double health = 300, double attack = 60) => new()
    {
        Index = index,
        Side = BattleSide.Enemies,
        Line = line,
        Key = $"enemy{index}",
        Attack = attack,
        Defense = 20,
        MaxHealth = health,
        Health = health,
        Speed = speed,
        Statuses = [],
        UniqueStats = [],
    };

    private static BattleState State(params Combatant[] combatants)
        => BattleEngine.NextRound(new BattleState
        {
            Combatants = combatants.ToList(),
            Wave = 1,
            Round = 0,
            Queue = [],
            Seed = 42,
            TurnNumber = 0,
        });

    /// <summary>Удар по цілі; за замовчуванням активний і готовий просто зараз.</summary>
    private static CombatSkill Strike(string key = "strike", double multiplier = 1.8, int cooldown = 3, int left = 0,
        AbilityTarget target = AbilityTarget.SingleEnemy, bool ignoresLine = false, SkillKind kind = SkillKind.Active) => new()
    {
        Key = key,
        Kind = kind,
        Target = target,
        Cooldown = cooldown,
        CooldownLeft = left,
        DamageMultiplier = multiplier,
        IgnoresLine = ignoresLine,
    };

    private static CombatSkill Heal(string key = "mend", SkillKind kind = SkillKind.Active, int left = 0) => new()
    {
        Key = key,
        Kind = kind,
        Target = AbilityTarget.SingleAlly,
        Cooldown = 3,
        CooldownLeft = left,
        HealPercent = 0.3,
    };

    /// <summary>Вміння «Захист»: щит на себе + провокація, як warrior_*_guard у конфігу.</summary>
    private static CombatSkill Guard(int shieldTurns = 2) => new()
    {
        Key = "guard",
        Kind = SkillKind.Active,
        Target = AbilityTarget.Self,
        Cooldown = 3,
        CooldownLeft = 0,
        ShieldPercent = 0.25,
        ShieldTurns = shieldTurns,
        Status = BattleStatusKind.Taunt,
        StatusTurns = 2,
    };

    private static CombatSkill SkillOf(BattleState state, int index, string key)
        => state.Combatants[index].Skills.Single(s => s.Key == key);

    // ---------- Черга ходів ----------

    /// <summary>Швидкість вирішує порядок; за рівної швидкості — номер у складі, щоб бій лишався відтворюваним.</summary>
    [Fact]
    public void NextRound_ShouldOrderBySpeedThenIndex()
    {
        var state = State(Hero(0, speed: 5), Hero(1, speed: 12), Enemy(2, speed: 12), Enemy(3, speed: 1));

        Assert.Equal([1, 2, 0, 3], state.Queue);
    }

    [Fact]
    public void NextRound_ShouldSkipTheDead()
    {
        var state = State(Hero(0), Enemy(1));
        var dead = state with { Combatants = [state.Combatants[0] with { Health = 0 }, state.Combatants[1]] };

        Assert.Equal([1], BattleEngine.NextRound(dead).Queue);
    }

    // ---------- Лінії ----------

    /// <summary>Поки жива передня лінія, тил недосяжний — саме це робить танка потрібним.</summary>
    [Fact]
    public void CanTarget_ShouldProtectTheBackLine_WhileTheFrontStands()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var actor = state.Combatants[0];

        Assert.True(BattleEngine.CanTarget(state, actor, null, 1));
        Assert.False(BattleEngine.CanTarget(state, actor, null, 2));
    }

    [Fact]
    public void CanTarget_ShouldOpenTheBackLine_WhenTheFrontIsDown()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var fallen = state with { Combatants = [state.Combatants[0], state.Combatants[1] with { Health = 0 }, state.Combatants[2]] };

        Assert.True(BattleEngine.CanTarget(fallen, fallen.Combatants[0], null, 2));
    }

    /// <summary>Далекобійне вміння дістає тил попри живу передню лінію.</summary>
    [Fact]
    public void CanTarget_ShouldReachTheBackLine_WithALineIgnoringAbility()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));

        Assert.True(BattleEngine.CanTarget(state, state.Combatants[0], Strike(ignoresLine: true), 2));
    }

    /// <summary>Провокація перебиває лінію: удари йдуть у того, хто провокує, навіть із тилу.</summary>
    [Fact]
    public void CanTarget_ShouldForceTheTaunter()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var taunted = state with
        {
            Combatants =
            [
                state.Combatants[0],
                state.Combatants[1],
                state.Combatants[2] with { Statuses = [new BattleStatus { Kind = BattleStatusKind.Taunt, Magnitude = 0, TurnsLeft = 2 }] },
            ],
        };

        Assert.True(BattleEngine.CanTarget(taunted, taunted.Combatants[0], null, 2));
        Assert.False(BattleEngine.CanTarget(taunted, taunted.Combatants[0], null, 1));
    }

    // ---------- Законність ходу ----------

    /// <summary>Правило ліній живе в рушії: хід у тил повз живу передню лінію не виконується.</summary>
    [Fact]
    public void Execute_ShouldRefuse_AnUnreachableTarget()
    {
        var state = State(Hero(0), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Engine().Execute(state, 0, new BattleAction(null, 2)));

        Assert.Equal(RefusalReasons.DungeonTargetUnreachable.Key, refusal.Reason);
    }

    [Fact]
    public void Execute_ShouldRefuse_ASkillOnCooldown()
        => Assert.Throws<RequirementNotMetException>(() =>
            Engine().Execute(State(Hero(0, skills: Strike(left: 1)), Enemy(1)), 0, new BattleAction("strike", 1)));

    /// <summary>Чуже чи періодичне вміння не можна назвати дією — лише своє активне.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("pulse")]
    public void Execute_ShouldRefuse_ASkillThatIsNotTheActorsActive(string key)
    {
        var hero = Hero(0, skills: [Strike(), Strike("pulse", kind: SkillKind.Periodic)]);

        Assert.Throws<RequirementNotMetException>(() =>
            Engine().Execute(State(hero, Enemy(1)), 0, new BattleAction(key, 1)));
    }

    [Fact]
    public void Execute_ShouldRefuse_ASingleTargetActionWithoutATarget()
        => Assert.Throws<RequirementNotMetException>(() =>
            Engine().Execute(State(Hero(0), Enemy(1)), 0, new BattleAction(null, null)));

    /// <summary>Автобій іде тим самим шляхом: його вибір завжди законний.</summary>
    [Fact]
    public void Execute_ShouldAcceptTheAutoChoice()
    {
        var state = State(Hero(0, skills: Strike()), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));
        var engine = Engine();

        var action = engine.ChooseAuto(state, 0);
        var result = engine.Execute(state, 0, action);

        Assert.Equal("strike", result.Log.AbilityKey);
        Assert.Equal(1, result.State.TurnNumber);
    }

    // ---------- Удари й перезарядка ----------

    [Fact]
    public void Execute_BasicAttack_ShouldDealDamage()
    {
        var state = State(Hero(0), Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        var enemy = result.State.Combatants[1];
        Assert.True(enemy.Health < enemy.MaxHealth);
        Assert.Equal(1, result.State.TurnNumber);
    }

    [Fact]
    public void Execute_Skill_ShouldHitHarderThanABasicAttack()
    {
        var state = State(Hero(0, skills: Strike(multiplier: 2.0)), Enemy(1));
        var engine = Engine();

        var basic = engine.Execute(state, 0, new BattleAction(null, 1));
        var skill = engine.Execute(state, 0, new BattleAction("strike", 1));

        Assert.True(skill.State.Combatants[1].Health < basic.State.Combatants[1].Health);
    }

    /// <summary>
    /// Перезарядка 3 — «раз на три ходи героя»: після удару вміння чекає ще два ходи
    /// й готове на третьому.
    /// </summary>
    [Fact]
    public void Execute_UsedSkill_ShouldGoOnItsFullCooldown()
    {
        var state = State(Hero(0, skills: Strike(cooldown: 3)), Enemy(1, health: 5000));

        var result = Engine().Execute(state, 0, new BattleAction("strike", 1));

        Assert.Equal(2, SkillOf(result.State, 0, "strike").CooldownLeft);
    }

    [Fact]
    public void Execute_UnusedSkill_ShouldTickDownByOneTurn()
    {
        var state = State(Hero(0, skills: Strike(left: 2)), Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.Equal(1, SkillOf(result.State, 0, "strike").CooldownLeft);
    }

    /// <summary>Оглушення забирає хід, а не час: перезарядка йде й тоді.</summary>
    [Fact]
    public void Execute_Stunned_ShouldStillTickTheCooldown()
    {
        var stunned = Hero(0, skills: Strike(left: 2)) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Stun, Magnitude = 0, TurnsLeft = 1 }],
        };

        var result = Engine().Execute(State(stunned, Enemy(1)), 0, new BattleAction(null, 1));

        Assert.Equal(1, SkillOf(result.State, 0, "strike").CooldownLeft);
    }

    /// <summary>Артефакт на перезарядку скорочує її після удару, але не нижче «щоходу».</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 0)]
    public void Execute_CooldownReduction_ShouldShortenTheRecharge(double reduction, int expected)
    {
        var hero = Hero(0, unique: new Dictionary<DungeonStat, double> { [DungeonStat.CooldownReduction] = reduction },
            skills: Strike(cooldown: 3));

        var result = Engine().Execute(State(hero, Enemy(1, health: 5000)), 0, new BattleAction("strike", 1));

        Assert.Equal(expected, SkillOf(result.State, 0, "strike").CooldownLeft);
    }

    /// <summary>Вміння по площі б'є всіх живих ворогів, правило ліній на нього не діє.</summary>
    [Fact]
    public void Execute_AreaSkill_ShouldHitEveryEnemy()
    {
        var area = Strike("storm", multiplier: 1.2, target: AbilityTarget.AllEnemies);
        var state = State(Hero(0, skills: area), Enemy(1, BattleLine.Front), Enemy(2, BattleLine.Back));

        var result = Engine().Execute(state, 0, new BattleAction("storm", null));

        Assert.True(result.State.Combatants[1].Health < result.State.Combatants[1].MaxHealth);
        Assert.True(result.State.Combatants[2].Health < result.State.Combatants[2].MaxHealth);
        Assert.Equal(2, result.Log.Effects.Count);
    }

    // ---------- Періодичні вміння ----------

    /// <summary>Періодичне спрацьовує саме наприкінці ходу героя, і його наслідки підписані ключем.</summary>
    [Fact]
    public void Execute_ReadyPeriodic_ShouldFireOnItsOwnAndRecharge()
    {
        var pulse = Strike("pulse", multiplier: 1.5, cooldown: 4, kind: SkillKind.Periodic);
        var state = State(Hero(0, skills: pulse), Enemy(1, health: 5000));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.Null(result.Log.AbilityKey);
        Assert.Equal(2, result.Log.Effects.Count(e => e.TargetIndex == 1));
        Assert.Single(result.Log.Effects, e => e.SkillKey == "pulse");
        Assert.Equal(3, SkillOf(result.State, 0, "pulse").CooldownLeft);
    }

    [Fact]
    public void Execute_PeriodicOnCooldown_ShouldOnlyTickDown()
    {
        var pulse = Strike("pulse", cooldown: 4, left: 2, kind: SkillKind.Periodic);
        var state = State(Hero(0, skills: pulse), Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.DoesNotContain(result.Log.Effects, e => e.SkillKey == "pulse");
        Assert.Equal(1, SkillOf(result.State, 0, "pulse").CooldownLeft);
    }

    /// <summary>Оглушений не діє, тож і періодичне чекає — готовим — до наступного його ходу.</summary>
    [Fact]
    public void Execute_StunnedHero_ShouldKeepAReadyPeriodic()
    {
        var stunned = Hero(0, skills: Strike("pulse", kind: SkillKind.Periodic)) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Stun, Magnitude = 0, TurnsLeft = 1 }],
        };

        var result = Engine().Execute(State(stunned, Enemy(1)), 0, new BattleAction(null, 1));

        Assert.Empty(result.Log.Effects);
        Assert.True(SkillOf(result.State, 0, "pulse").Ready);
    }

    /// <summary>Періодичне лікування саме шукає найпораненішого союзника.</summary>
    [Fact]
    public void Execute_PeriodicHeal_ShouldPickTheMostHurtAlly()
    {
        var hurt = Hero(1) with { Health = 100 };
        var state = State(Hero(0, skills: Heal(kind: SkillKind.Periodic)), hurt, Enemy(2));

        var result = Engine().Execute(state, 0, new BattleAction(null, 2));

        Assert.Equal(250, result.State.Combatants[1].Health);
    }

    // ---------- Підготовка вміння ----------

    /// <summary>Рівень вшивається в множники, а відлік повний: перезарядка 3 — перший удар на третьому ході.</summary>
    [Fact]
    public void Prepare_ShouldScaleByLevel_AndStartOnCooldown()
    {
        var battle = new SkillBattleConfig
        {
            Target = AbilityTarget.SingleEnemy, Cooldown = 3, DamageMultiplier = 1.5, HealPercent = 0.2,
        };

        var skill = CombatSkill.Prepare("smash", SkillKind.Active, battle, levelScale: 2.0);

        Assert.Equal(3.0, skill.DamageMultiplier, 6);
        Assert.Equal(0.4, skill.HealPercent, 6);
        Assert.Equal(2, skill.CooldownLeft);
    }

    // ---------- Підтримка ----------

    [Fact]
    public void Execute_Heal_ShouldRestoreHealthWithoutExceedingTheMaximum()
    {
        var hurt = Hero(0) with { Health = 100 };
        var state = State(hurt, Hero(1, skills: Heal()), Enemy(2));

        var result = Engine().Execute(state, 1, new BattleAction("mend", 0));

        Assert.Equal(250, result.State.Combatants[0].Health);
    }

    /// <summary>Щит поглинає шкоду до свого запасу, а решта йде у здоров'я.</summary>
    [Fact]
    public void Execute_Shield_ShouldAbsorbDamageFirst()
    {
        var shielded = Hero(0) with
        {
            ShieldPoints = 1000,
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Shield, Magnitude = 0, TurnsLeft = 3 }],
        };
        var state = State(shielded, Enemy(1));

        var result = Engine().Execute(state, 1, new BattleAction(null, 0));

        var hero = result.State.Combatants[0];
        Assert.Equal(hero.MaxHealth, hero.Health);
        Assert.True(hero.ShieldPoints < 1000);
        Assert.True(result.Log.Effects[0].ShieldAbsorbed > 0);
    }

    /// <summary>
    /// Щит на себе переживає власний хід: стан тікає наприкінці ходу носія, і без
    /// статусу Shield щит обнулився б одразу — вміння не працювало б узагалі.
    /// </summary>
    [Fact]
    public void Execute_ShieldOnSelf_ShouldSurviveTheOwnTurnAndAbsorbTheNextHit()
    {
        var state = State(Hero(0, skills: Guard()), Enemy(1));

        var guarded = Engine().Execute(state, 0, new BattleAction("guard", 0));
        var hero = guarded.State.Combatants[0];

        Assert.Equal(125, hero.ShieldPoints);
        Assert.Contains(hero.Statuses, st => st.Kind == BattleStatusKind.Shield);

        var hit = Engine().Execute(guarded.State, 1, new BattleAction(null, 0));

        Assert.True(hit.Log.Effects[0].ShieldAbsorbed > 0);
    }

    /// <summary>Щит згасає разом зі своїм станом — через ShieldTurns ходів носія.</summary>
    [Fact]
    public void Execute_Shield_ShouldFadeAfterItsTurns()
    {
        var state = State(Hero(0, skills: Guard(shieldTurns: 2)), Enemy(1));

        var afterGuard = Engine().Execute(state, 0, new BattleAction("guard", 0)).State;
        var afterNextTurn = Engine().Execute(afterGuard, 0, new BattleAction(null, 1)).State;

        Assert.Equal(0, afterNextTurn.Combatants[0].ShieldPoints);
    }

    /// <summary>Повторне накладання оновлює тривалість, а очки не сумуються — інакше запас ріс би без меж.</summary>
    [Fact]
    public void Execute_Shield_ShouldNotStackOnRecast()
    {
        var state = State(Hero(0, skills: Guard()), Enemy(1));

        var once = Engine().Execute(state, 0, new BattleAction("guard", 0)).State;
        var recharged = once with { Combatants = [once.Combatants[0] with { Skills = [Guard()] }, once.Combatants[1]] };
        var twice = Engine().Execute(recharged, 0, new BattleAction("guard", 0)).State;

        Assert.Equal(125, twice.Combatants[0].ShieldPoints);
    }

    // ---------- Стани ----------

    [Fact]
    public void Execute_ShouldSkipTheTurn_WhenStunned()
    {
        var stunned = Hero(0) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Stun, Magnitude = 0, TurnsLeft = 1 }],
        };
        var state = State(stunned, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.True(result.Log.Stunned);
        Assert.Equal(result.State.Combatants[1].MaxHealth, result.State.Combatants[1].Health);
        // Стан згорає наприкінці ходу — наступний хід уже вільний
        Assert.Empty(result.State.Combatants[0].Statuses);
    }

    [Fact]
    public void Execute_Poison_ShouldBiteBeforeTheTurnAndExpire()
    {
        var poisoned = Hero(0) with
        {
            Statuses = [new BattleStatus { Kind = BattleStatusKind.Poison, Magnitude = 40, TurnsLeft = 1 }],
        };
        var state = State(poisoned, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.Equal(460, result.State.Combatants[0].Health);
        Assert.Empty(result.State.Combatants[0].Statuses);
    }

    /// <summary>Повторне накладання оновлює тривалість, а не складає ефекти.</summary>
    [Fact]
    public void Execute_ShouldRefreshAStatus_InsteadOfStacking()
    {
        var venom = Strike("venom", multiplier: 1.0) with
        {
            Status = BattleStatusKind.Poison, StatusMagnitude = 30, StatusTurns = 3,
        };
        var state = State(Hero(0, skills: venom), Enemy(1, health: 5000));
        var engine = Engine();

        var first = engine.Execute(state, 0, new BattleAction("venom", 1)).State;
        var recharged = first with { Combatants = [first.Combatants[0] with { Skills = [venom] }, first.Combatants[1]] };
        var second = engine.Execute(recharged, 0, new BattleAction("venom", 1));

        var poison = second.State.Combatants[1].Statuses.Where(s => s.Kind == BattleStatusKind.Poison).ToList();
        Assert.Single(poison);
        Assert.Equal(3, poison[0].TurnsLeft);
    }

    // ---------- Унікальні стати артефактів ----------

    [Fact]
    public void Execute_Lifesteal_ShouldHealTheAttacker()
    {
        var vampire = Hero(0, unique: new Dictionary<DungeonStat, double> { [DungeonStat.Lifesteal] = 0.5 }) with { Health = 200 };
        var state = State(vampire, Enemy(1));

        var result = Engine().Execute(state, 0, new BattleAction(null, 1));

        Assert.True(result.State.Combatants[0].Health > 200);
    }

    [Fact]
    public void Execute_DamageReduction_ShouldSoftenTheHit()
    {
        var plain = State(Hero(0), Enemy(1));
        var tough = State(Hero(0, unique: new Dictionary<DungeonStat, double> { [DungeonStat.DamageReduction] = 0.5 }), Enemy(1));
        var engine = Engine();

        var hitPlain = engine.Execute(plain, 1, new BattleAction(null, 0)).State.Combatants[0].Health;
        var hitTough = engine.Execute(tough, 1, new BattleAction(null, 0)).State.Combatants[0].Health;

        Assert.True(hitTough > hitPlain);
    }

    // ---------- Детермінізм ----------

    /// <summary>Той самий стан і сід дають той самий хід — інакше бій не відтворити з журналу.</summary>
    [Fact]
    public void Execute_ShouldBeReproducible_ForTheSameSeed()
    {
        var config = Config();
        config.BaseCritChance = 0.5;

        var state = State(Hero(0), Enemy(1));

        var first = new BattleEngine(config).Execute(state, 0, new BattleAction(null, 1));
        var second = new BattleEngine(config).Execute(state, 0, new BattleAction(null, 1));

        Assert.Equal(first.State.Combatants[1].Health, second.State.Combatants[1].Health);
        Assert.Equal(first.Log.Effects[0].Critical, second.Log.Effects[0].Critical);
    }

    // ---------- Автобій ----------

    [Fact]
    public void ChooseAuto_ShouldUseTheActiveSkill_WhenItIsReady()
    {
        var state = State(Hero(0, skills: Strike()), Enemy(1));

        var action = Engine().ChooseAuto(state, 0);

        Assert.Equal("strike", action.AbilityKey);
    }

    [Fact]
    public void ChooseAuto_ShouldFallBackToABasicAttack_WhileTheSkillRecharges()
    {
        var state = State(Hero(0, skills: Strike(left: 1)), Enemy(1));

        var action = Engine().ChooseAuto(state, 0);

        Assert.Null(action.AbilityKey);
        Assert.Equal(1, action.TargetIndex);
    }

    /// <summary>Лікування в повну команду — змарнована перезарядка, тож автобій його не бере.</summary>
    [Fact]
    public void ChooseAuto_ShouldNotHeal_AHealthyTeam()
    {
        var state = State(Hero(0, skills: Heal()), Enemy(1));

        Assert.Null(Engine().ChooseAuto(state, 0).AbilityKey);
    }

    [Fact]
    public void ChooseAuto_ShouldFinishOffTheWeakestReachableEnemy()
    {
        var state = State(Hero(0), Enemy(1, health: 300), Enemy(2, health: 80));

        var action = Engine().ChooseAuto(state, 0);

        Assert.Equal(2, action.TargetIndex);
    }

    // ---------- Стеля раундів ----------

    /// <summary>Стеля перевіряється на старті нового раунду: MaxRoundsPerWave повних раундів дозволено, наступний — ні.</summary>
    [Theory]
    [InlineData(29, false)]
    [InlineData(30, false)]
    [InlineData(31, true)]
    public void IsOutOfRounds_ShouldTripOnlyPastTheCeiling(int round, bool expected)
    {
        var config = Config();
        config.MaxRoundsPerWave = 30;
        var state = State(Hero(0), Enemy(1)) with { Round = round };

        Assert.Equal(expected, Engine(config).IsOutOfRounds(state));
    }
}
