using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;
using BattleSide = EmpireIdle.Domain.Battles.BattleSide;

namespace EmpireIdle.Domain.Tests.Battles;

/// <summary>
/// Вміння в одномоментному бою: хід данжу — 2.5 с, перше спрацювання — на половині перезарядки,
/// стани тримаються секундами. Тик — 100 мс, дальні б'ють уперше на 5-му тику.
/// </summary>
public class RealtimeBattleSkillsTests
{
    private static readonly RealtimeBattleConfig Config = new()
    {
        TickMilliseconds = 100,
        MaxBattleSeconds = 90,
        HeroLines = new() { ["warrior"] = 0, ["knight"] = 1, ["archer"] = 2 },
        RangedClasses = ["archer"],
        MeleeApproachSeconds = 1.5,
        RangedApproachSeconds = 0.5,
        DefensePerHalfDamage = 2,
        SecondsPerTurn = 2.5,
        FirstCastShare = 0.5,
        AoeShare = 0.4,
        WardSeconds = 15,
        SquadSize = 5
    };

    private readonly RealtimeBattleEngine _engine = new(Config);

    private static FighterStats Stats(double attack = 0, double health = 1_000_000, double interval = 1000)
        => FighterStats.Plain(attack, 0, health, interval);

    private static CombatSkill Skill(AbilityTarget target, int cooldown = 2, double damage = 0, SkillKind kind = SkillKind.Active,
        double heal = 0, double shield = 0, int shieldTurns = 0, BattleStatusKind? status = null, double magnitude = 0, int statusTurns = 0,
        SkillFocus focus = SkillFocus.Nearest)
        => CombatSkill.Prepare($"skill_{target}_{status}", kind, new SkillBattleConfig
        {
            Target = target,
            Cooldown = cooldown,
            DamageMultiplier = damage,
            HealPercent = heal,
            ShieldPercent = shield,
            ShieldTurns = shieldTurns,
            Status = status,
            StatusMagnitude = magnitude,
            StatusTurns = statusTurns,
            Focus = focus
        }, levelScale: 1);

    private static SquadMember Archer(int lane, FighterStats stats, CombatSkill[]? skills = null, params Convoy[] convoys)
        => new(lane, "archer", "archer", stats, convoys, skills);

    private static SquadMember Warrior(int lane, FighterStats stats) => new(lane, "warrior", "infantry", stats, []);

    private static SquadMember Knight(int lane, FighterStats stats) => new(lane, "knight", "spearman", stats, []);

    private static Convoy Convoy(FighterStats stats) => new("archer", 100, stats, stats);

    private static int SkillTarget(BattleResult result, int source)
        => Events(result, BattleEventKind.Hit, source).First(e => e.Flags.HasFlag(HitFlags.Skill)).Target;

    private BattleResult Run(SquadMember[] attackers, SquadMember[] defenders) => _engine.Run(new BattleSetup(attackers, defenders), seed: 1);

    private static List<BattleEvent> Events(BattleResult result, BattleEventKind kind, int source)
        => result.Events.Where(e => e.Kind == kind && e.Source == source).ToList();

    /// <summary>Перезарядка 2 ходи = 5 с: уперше на 2.5 с, далі кожні 5 с; удар уміння ×2 від атаки.</summary>
    [Fact]
    public void Run_ShouldCastOnTimer_FromHalfTheCooldown()
    {
        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 2)])], [Archer(0, Stats())]);

        Assert.Equal([25, 75, 125], Events(result, BattleEventKind.Cast, 0).Take(3).Select(e => e.Tick));

        var skillHit = Events(result, BattleEventKind.Hit, 0).First(e => e.Flags.HasFlag(HitFlags.Skill));
        Assert.Equal((25, 100.0), (skillHit.Tick, skillHit.Amount));
        Assert.Equal(["skill_SingleEnemy_"], result.Fighters[0].Skills);
    }

    private BattleResult RunManual(SquadMember[] attackers, SquadMember[] defenders, params ManualCast[] casts)
        => _engine.Run(new BattleSetup(attackers, defenders, casts), seed: 1);

    /// <summary>Ручний бій: готове активне вміння не спрацьовує саме — лише повідомляє, що готове.</summary>
    [Fact]
    public void Run_ShouldHoldTheActiveSkill_InAManualBattle()
    {
        var result = RunManual([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 2)])], [Archer(0, Stats())]);

        Assert.Empty(Events(result, BattleEventKind.Cast, 0));
        Assert.Equal([25], Events(result, BattleEventKind.Ready, 0).Select(e => e.Tick));
    }

    /// <summary>Наказ гравця кастує в свій тик, і перезарядка відлічується від нього.</summary>
    [Fact]
    public void Run_ShouldCastOnTheOrder_AndCoolDownFromIt()
    {
        var result = RunManual([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 2)])], [Archer(0, Stats())],
            new ManualCast(40, 0));

        Assert.Equal([40], Events(result, BattleEventKind.Cast, 0).Select(e => e.Tick));
        Assert.Equal([25, 90], Events(result, BattleEventKind.Ready, 0).Select(e => e.Tick));
    }

    /// <summary>Наказ раніше готовності не пропадає: вміння спрацює, щойно перезарядиться.</summary>
    [Fact]
    public void Run_ShouldKeepAnEarlyOrder_UntilTheSkillIsReady()
    {
        var result = RunManual([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 2)])], [Archer(0, Stats())],
            new ManualCast(10, 0));

        Assert.Equal([25], Events(result, BattleEventKind.Cast, 0).Select(e => e.Tick));
    }

    /// <summary>Вручну — лише активні: періодичне вміння гравця й усі вміння ворога б'ють самі.</summary>
    [Fact]
    public void Run_ShouldKeepPeriodicAndEnemySkillsAutomatic_InAManualBattle()
    {
        var periodic = Skill(AbilityTarget.SingleEnemy, damage: 1, kind: SkillKind.Periodic);

        var result = RunManual([Archer(0, Stats(attack: 50), [periodic])], [Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 1)])]);

        Assert.Equal(25, Events(result, BattleEventKind.Cast, 0)[0].Tick);
        Assert.Equal(25, Events(result, BattleEventKind.Cast, 1)[0].Tick);
        Assert.Empty(result.Events.Where(e => e.Kind == BattleEventKind.Ready));
    }

    /// <summary>Вміння на тил б'є ворожого стрільця на далекій доріжці, хоч найближчий — воїн навпроти.</summary>
    [Fact]
    public void Run_ShouldAimTheSkillAtTheBackLine()
    {
        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 1, focus: SkillFocus.BackLine)])],
            [Warrior(0, Stats()), Archer(4, Stats())]);

        // 0 — нападник; 1 — воїн, 2 — стрілець
        Assert.Equal(2, SkillTarget(result, 0));
    }

    /// <summary>Вміння на середню лінію дістає лицаря за спинами воїнів.</summary>
    [Fact]
    public void Run_ShouldAimTheSkillAtTheMiddleLine()
    {
        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 1, focus: SkillFocus.MiddleLine)])],
            [Warrior(0, Stats()), Knight(3, Stats())]);

        Assert.Equal(2, SkillTarget(result, 0));
    }

    /// <summary>«Найсильніший» — герой з найбільшою атакою, хоч би де стояв.</summary>
    [Fact]
    public void Run_ShouldAimTheSkillAtTheStrongestHero()
    {
        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 1, focus: SkillFocus.Strongest)])],
            [Warrior(0, Stats(attack: 10)), Warrior(4, Stats(attack: 99))]);

        Assert.Equal(2, SkillTarget(result, 0));
    }

    /// <summary>Обраної лінії немає — вміння б'є найближчого, а не згорає.</summary>
    [Fact]
    public void Run_ShouldFallBackToTheNearest_WhenTheLineIsGone()
    {
        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.SingleEnemy, damage: 1, focus: SkillFocus.MiddleLine)])],
            [Warrior(0, Stats()), Archer(4, Stats())]);

        Assert.Equal(1, SkillTarget(result, 0));
    }

    /// <summary>Скорочення перезарядки на 20% — і для першого спрацювання, і для наступних; шкода вмінь множить удар.</summary>
    [Fact]
    public void Run_ShouldShortenTheCooldown_AndStrengthenTheSkill()
    {
        var hero = Stats(attack: 50) with { CooldownMultiplier = 0.8, SkillDamage = 0.5 };

        var result = Run([Archer(0, hero, [Skill(AbilityTarget.SingleEnemy, damage: 2)])], [Archer(0, Stats())]);

        Assert.Equal([20, 60], Events(result, BattleEventKind.Cast, 0).Take(2).Select(e => e.Tick));
        Assert.Equal(150, Events(result, BattleEventKind.Hit, 0).First(e => e.Flags.HasFlag(HitFlags.Skill)).Amount, 6);
    }

    /// <summary>Пасивка в бою не спрацьовує, а періодичне б'є само, як і активне.</summary>
    [Fact]
    public void Run_ShouldFirePeriodicSkills_AndSkipPassives()
    {
        CombatSkill[] skills = [Skill(AbilityTarget.SingleEnemy, damage: 1, kind: SkillKind.Passive), Skill(AbilityTarget.SingleEnemy, damage: 1, kind: SkillKind.Periodic)];

        var result = Run([Archer(0, Stats(attack: 50), skills)], [Archer(0, Stats())]);

        Assert.Single(result.Fighters[0].Skills);
        Assert.Equal(25, Events(result, BattleEventKind.Cast, 0)[0].Tick);
    }

    /// <summary>Масове вміння б'є загін цілі — героя й конвої — на 40%, а сусідню доріжку не чіпає.</summary>
    [Fact]
    public void Run_ShouldHitTheTargetsSquad_WithAnAreaSkill()
    {
        var defenders = new[] { Archer(0, Stats(), null, Convoy(Stats()), Convoy(Stats())), Archer(1, Stats()) };

        var result = Run([Archer(0, Stats(attack: 50), [Skill(AbilityTarget.AllEnemies, damage: 1)])], defenders);

        var area = Events(result, BattleEventKind.Hit, 0).Where(e => e.Tick == 25).ToList();
        Assert.Equal([1, 2, 3], area.Select(e => e.Target));
        Assert.All(area, e => Assert.Equal(20, e.Amount, 6));
    }

    /// <summary>Лікування дістається найпораненішому союзнику, часткою його максимального здоров'я.</summary>
    [Fact]
    public void Run_ShouldHealTheMostWoundedAlly()
    {
        var attackers = new[]
        {
            new SquadMember(0, "warrior", "infantry", Stats(health: 1000), []),
            Archer(4, Stats(health: 1000), [Skill(AbilityTarget.SingleAlly, heal: 0.1)])
        };

        // Лучник захисту на 5-му тику б'є воїна на найближчій доріжці на 300
        var result = Run(attackers, [Archer(0, Stats(attack: 300))]);

        Assert.Contains(new BattleEvent(25, BattleEventKind.Heal, 1, 0, 100, Health: 800), result.Events);
    }

    /// <summary>
    /// Щит на себе 20% від 1000 — на 5 с, поновлюється кожні 5 с. Ворог б'є по 100 щосекунди: без щита
    /// герой ліг би на 95-му тику, а так щит щоразу ковтає два удари.
    /// </summary>
    [Fact]
    public void Run_ShouldAbsorbHits_WithASkillShield()
    {
        var result = Run([Archer(0, Stats(health: 1000), [Skill(AbilityTarget.Self, shield: 0.2, shieldTurns: 2)])], [Archer(0, Stats(attack: 100, interval: 1))]);

        Assert.Contains(new BattleEvent(25, BattleEventKind.Shield, 0, 0, 200), result.Events);
        Assert.Equal((BattleSide.Defender, 155), (result.Winner, result.Ticks));
    }

    /// <summary>
    /// Оглушення на 1 хід (2.5 с) зупиняє і удари, і накопичувач: ворог, що бив щосекунди, після 25-го тику
    /// мовчить до 50-го й б'є знову лише через повну секунду.
    /// </summary>
    [Fact]
    public void Run_ShouldStopAStunnedHero()
    {
        var stun = Skill(AbilityTarget.SingleEnemy, damage: 1, status: BattleStatusKind.Stun, statusTurns: 1);

        var result = Run([Archer(0, Stats(attack: 1), [stun])], [Archer(0, Stats(attack: 10, interval: 1))]);

        Assert.Equal([5, 15, 25, 59, 69], Events(result, BattleEventKind.Hit, 1).Take(5).Select(e => e.Tick));
        Assert.Contains(new BattleEvent(25, BattleEventKind.Status, 1, 1, 2.5, Detail: (int)BattleStatusKind.Stun), result.Events);
    }

    /// <summary>Конвой не оглушити: масове оглушення лягає лише на героя загону.</summary>
    [Fact]
    public void Run_ShouldNotStunConvoys()
    {
        var stun = Skill(AbilityTarget.AllEnemies, damage: 1, status: BattleStatusKind.Stun, statusTurns: 1);

        var result = Run([Archer(0, Stats(attack: 1), [stun])], [Archer(0, Stats(), null, Convoy(Stats()))]);

        var statuses = result.Events.Where(e => e.Kind == BattleEventKind.Status).Select(e => e.Target).Distinct();
        Assert.Equal([1], statuses);
    }

    /// <summary>
    /// Отрута 0.5 атаки за хід — 100 × 0.5 / 2.5 = 20 щосекунди; на 2 ходи (5 с) — чотири укуси,
    /// перший через секунду після накладання; наступна отрута — з другим спрацюванням на 75-й секунді.
    /// </summary>
    [Fact]
    public void Run_ShouldPoisonOncePerSecond()
    {
        var poison = Skill(AbilityTarget.SingleEnemy, cooldown: 20, damage: 1, status: BattleStatusKind.Poison, magnitude: 0.5, statusTurns: 2);

        var result = Run([Archer(0, Stats(attack: 100), [poison])], [Archer(0, Stats())]);

        var bites = result.Events.Where(e => e.Flags.HasFlag(HitFlags.Poison)).Take(5).ToList();
        Assert.Equal([260, 270, 280, 290, 760], bites.Select(e => e.Tick));
        Assert.All(bites, e => Assert.Equal((1, 20.0), (e.Target, e.Amount)));
    }

    /// <summary>Провокація перетягує ворожого героя на себе понад липку ціль; конвої її не чують.</summary>
    [Fact]
    public void Run_ShouldPullEnemyHeroes_WithATaunt()
    {
        var defenders = new[]
        {
            Archer(0, Stats()),
            new SquadMember(4, "knight", "spearman", Stats(), [], [Skill(AbilityTarget.Self, status: BattleStatusKind.Taunt, statusTurns: 2)])
        };

        var result = Run([Archer(0, Stats(attack: 1, interval: 1), null, Convoy(Stats(attack: 1, interval: 1)))], defenders);

        Assert.Equal([2, 2, 2, 3], Events(result, BattleEventKind.Hit, 0).Take(4).Select(e => e.Target));
        Assert.Equal([2, 2, 2, 2], Events(result, BattleEventKind.Hit, 1).Take(4).Select(e => e.Target));
    }
}
