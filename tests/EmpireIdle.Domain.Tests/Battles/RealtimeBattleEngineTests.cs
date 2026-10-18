using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Battles;

/// <summary>
/// Одномоментний бій: темп ударів, формула захисту, перевага типів, вибір цілей, загибель героя
/// й кінець бою. Тик — 100 мс; ближні доходять за 1.5 с (15-й тик), дальні — за 0.5 с (5-й).
/// </summary>
public class RealtimeBattleEngineTests
{
    private const string Infantry = "infantry";
    private const string Archer = "archer";

    private static readonly RealtimeBattleConfig Config = new()
    {
        TickMilliseconds = 100,
        MaxBattleSeconds = 90,
        FrontLineClasses = ["warrior", "knight"],
        MeleeApproachSeconds = 1.5,
        RangedApproachSeconds = 0.5,
        DefensePerHalfDamage = 2,
        TypeAdvantages = new() { [Infantry] = Archer, [Archer] = "spearman", ["spearman"] = Infantry },
        TypeAdvantageMultiplier = 1.35,
        SquadSize = 5
    };

    private readonly RealtimeBattleEngine _engine = new(Config);

    private static FighterStats Stats(double attack = 0, double defense = 0, double health = 1_000_000, double interval = 1)
        => FighterStats.Plain(attack, defense, health, interval);

    private static SquadMember ArcherHero(int lane, FighterStats stats, params Convoy[] convoys)
        => new(lane, "archer", Archer, stats, convoys);

    private static SquadMember WarriorHero(int lane, FighterStats stats, params Convoy[] convoys)
        => new(lane, "warrior", Infantry, stats, convoys);

    private static Convoy Convoy(string unitType, FighterStats led, FighterStats? leaderless = null)
        => new(unitType, 100, led, leaderless ?? led);

    private BattleResult Run(SquadMember[] attackers, SquadMember[] defenders) => _engine.Run(new BattleSetup(attackers, defenders));

    private static List<BattleEvent> HitsBy(BattleResult result, int source)
        => result.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source == source).ToList();

    /// <summary>Дальній б'є вперше, щойно прицілився, далі — кожні AttackInterval секунд.</summary>
    [Fact]
    public void Run_ShouldStrikeRightAfterTheApproach_ThenOnEveryInterval()
    {
        var result = Run([ArcherHero(0, Stats(attack: 10, interval: 1))], [ArcherHero(0, Stats())]);

        Assert.Equal([5, 15, 25, 35], HitsBy(result, 0).Take(4).Select(e => e.Tick));
    }

    /// <summary>Ближньому треба дійти: перший удар на 15-му тику; темп 1.25 с — наступний через 12–13 тиків.</summary>
    [Fact]
    public void Run_ShouldLetMeleeFightersWalkFirst()
    {
        var result = Run([WarriorHero(0, Stats(attack: 10, interval: 1.25))], [WarriorHero(0, Stats())]);

        Assert.Equal([15, 28, 40, 53, 65], HitsBy(result, 0).Take(5).Select(e => e.Tick));
    }

    /// <summary>Захист удвічі більший за атаку пропускає половину удару; без захисту — увесь.</summary>
    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 100 * 100 / 150.0)]
    [InlineData(200, 50)]
    [InlineData(400, 100 / 3.0)]
    public void Run_ShouldSoftenTheHitByDefense(double defense, double expected)
    {
        var result = Run([ArcherHero(0, Stats(attack: 100))], [ArcherHero(0, Stats(defense: defense))]);

        Assert.Equal(expected, HitsBy(result, 0)[0].Amount, 6);
    }

    /// <summary>Піхота б'є лучників на 35% сильніше, лучники піхоту — слабше в ту саму міру.</summary>
    [Fact]
    public void Run_ShouldApplyTheTypeAdvantageBothWays()
    {
        var result = Run([ArcherHero(0, Stats(attack: 100))], [WarriorHero(0, Stats(attack: 100))]);

        var archerHit = HitsBy(result, 0)[0];
        var warriorHit = HitsBy(result, 1)[0];

        Assert.Equal((100 / 1.35, HitFlags.Disadvantage), (archerHit.Amount, archerHit.Flags));
        Assert.Equal(100 * 1.35, warriorHit.Amount, 6);
        Assert.Equal(HitFlags.Advantage, warriorHit.Flags);
    }

    /// <summary>Третій удар по 100 кладе ворога з 300 здоров'я — бій кінчається в тому ж тику.</summary>
    [Fact]
    public void Run_ShouldEndTheBattle_WhenTheLastEnemyFalls()
    {
        var result = Run([ArcherHero(0, Stats(attack: 100))], [ArcherHero(0, Stats(health: 300))]);

        Assert.Equal((BattleSide.Attacker, 25), (result.Winner, result.Ticks));
        Assert.Contains(new BattleEvent(25, BattleEventKind.Death, 1, 1, 0), result.Events);
        Assert.Equal([1_000_000, 0], result.Health);
    }

    /// <summary>
    /// Удари тику застосовуються разом: у дзеркальному бою обидва лягають в одному тику, хоч нападника
    /// обробляють першим. Нічия — перемога захисника.
    /// </summary>
    [Fact]
    public void Run_ShouldResolveTheTickAtOnce_AndGiveADrawToTheDefender()
    {
        var result = Run([ArcherHero(0, Stats(attack: 100, health: 300))], [ArcherHero(0, Stats(attack: 100, health: 300))]);

        Assert.Equal((BattleSide.Defender, 25), (result.Winner, result.Ticks));
        Assert.Equal(2, result.Events.Count(e => e.Kind == BattleEventKind.Death));
    }

    /// <summary>Ніхто нікого не пробив до стелі 90 с — нападник не дотиснув.</summary>
    [Fact]
    public void Run_ShouldGiveTheBattleToTheDefender_OnTimeout()
    {
        var result = Run([ArcherHero(0, Stats())], [ArcherHero(0, Stats())]);

        Assert.Equal((BattleSide.Defender, 900), (result.Winner, result.Ticks));
    }

    /// <summary>
    /// Ближній дістає лише фронт, поки там хтось стоїть: тил на своїй доріжці пропускає й іде на сусідню,
    /// де першим б'є найслабший конвой, а не героя.
    /// </summary>
    [Fact]
    public void Run_ShouldSendMeleeAtTheFrontLine_ConvoysFirst()
    {
        var defenders = new[]
        {
            ArcherHero(0, Stats(), Convoy(Archer, Stats())),
            WarriorHero(1, Stats(health: 100), Convoy(Infantry, Stats(health: 500)), Convoy(Infantry, Stats(health: 300)))
        };

        var result = Run([WarriorHero(0, Stats(attack: 1))], defenders);

        // 0 — нападник; 1, 2 — лучник і його конвой; 3 — воїн, 4 і 5 — його конвої
        Assert.Equal(5, HitsBy(result, 0)[0].Target);
    }

    /// <summary>Герой-лучник — снайпер: на найближчій доріжці першим цілить героя.</summary>
    [Fact]
    public void Run_ShouldLetArcherHeroesSnipeHeroes()
    {
        var defenders = new[]
        {
            ArcherHero(0, Stats(), Convoy(Archer, Stats(health: 10))),
            WarriorHero(1, Stats(), Convoy(Infantry, Stats(health: 10)))
        };

        var result = Run([ArcherHero(0, Stats(attack: 1))], defenders);

        Assert.Equal(1, HitsBy(result, 0)[0].Target);
    }

    /// <summary>Конвой лучників стріляє через фронт, але, як і всі, спершу по конвоях.</summary>
    [Fact]
    public void Run_ShouldLetArcherConvoysShootOverTheFront_ConvoysFirst()
    {
        var defenders = new[]
        {
            WarriorHero(0, Stats(), Convoy(Infantry, Stats())),
            ArcherHero(3, Stats(), Convoy(Archer, Stats()))
        };

        var result = Run([ArcherHero(4, Stats(), Convoy(Archer, Stats(attack: 1)))], defenders);

        Assert.Equal(5, HitsBy(result, 1)[0].Target);
    }

    /// <summary>Ціль липка: боєць не перемикається на слабшого, поки перша ціль жива.</summary>
    [Fact]
    public void Run_ShouldKeepTheTarget_UntilItFalls()
    {
        var defenders = new[] { ArcherHero(0, Stats(), Convoy(Archer, Stats(health: 250)), Convoy(Archer, Stats(health: 260))) };

        var result = Run([ArcherHero(1, Stats(), Convoy(Archer, Stats(attack: 100)))], defenders);

        // Перший конвой (250) падає з третього удару; поки він живий, другий (260) не отримує нічого
        Assert.Equal([3, 3, 3, 4], HitsBy(result, 1).Take(4).Select(e => e.Target));
    }

    /// <summary>Загинув герой — його конвої б'ються далі, але вже без його бонусів.</summary>
    [Fact]
    public void Run_ShouldStripTheHerosBonuses_FromConvoysOfAFallenHero()
    {
        var attackers = new[] { ArcherHero(0, Stats(health: 1), Convoy(Archer, Stats(attack: 200), Stats(attack: 100))) };

        var result = Run(attackers, [ArcherHero(0, Stats(attack: 10))]);

        Assert.Contains(new BattleEvent(5, BattleEventKind.Death, 0, 0, 0), result.Events);
        Assert.Equal([200.0, 100, 100], HitsBy(result, 1).Take(3).Select(e => e.Amount));
    }

    /// <summary>Той самий загін проти того самого ворога — той самий бій до удару.</summary>
    [Fact]
    public void Run_ShouldBeDeterministic()
    {
        SquadMember[] Squad() =>
        [
            WarriorHero(0, Stats(attack: 90, defense: 40, health: 2000, interval: 1.25), Convoy(Infantry, Stats(attack: 30, health: 900, interval: 1.25))),
            ArcherHero(2, Stats(attack: 120, defense: 20, health: 1500, interval: 1.3), Convoy(Archer, Stats(attack: 25, health: 600, interval: 1.4)))
        ];

        var first = Run(Squad(), Squad());
        var second = Run(Squad(), Squad());

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Winner, second.Winner);
    }

    [Fact]
    public void Run_ShouldRefuseTwoHeroesOnOneLane()
        => Assert.Throws<ArgumentException>(() => Run([ArcherHero(1, Stats()), WarriorHero(1, Stats())], [ArcherHero(0, Stats())]));
}
