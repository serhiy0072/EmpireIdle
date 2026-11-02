using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Technologies;

/// <summary>
/// Дерево технологій (рішення 28.10.2026): гейти тіру, передумови, перехід на наступний тір,
/// ціна й час особистого рівня, очки кланового, монети за підношення.
/// Фікстура з TestKit: тір 1 — ратуша 5 і світ 1, тір 2 — ратуша 10 і світ 2;
/// у бою attack_infantry (3 рівні, +1% атаки піхоти, шкала Battle) → command_infantry (2 рівні, +4 у конвой).
/// </summary>
public class TechnologyTreeTests
{
    private static readonly TechnologiesConfig Config = new GameConfigBuilder().WithTechnologies().Build().Technologies;
    private static readonly PersonalTechnologies Personal = new(Config.Personal);
    private static readonly ClanTechnologies Clan = new(Config.Clan);

    private static Dictionary<string, int> Levels(params (int Tier, string Node, int Level)[] levels)
        => levels.ToDictionary(l => new TechnologyKey(l.Tier, l.Node).ToString(), l => l.Level);

    [Fact]
    public void Availability_ShouldOpenTheFirstRow_WhenTheTierGateIsMet()
        => Assert.Equal(TechnologyAvailability.Available,
            Personal.Tree.Availability(Levels(), 1, "attack_infantry", mainBuildingLevel: 5, worldLevel: 1));

    [Fact]
    public void Availability_ShouldWaitForTheTownHall()
        => Assert.Equal(TechnologyAvailability.TierGated,
            Personal.Tree.Availability(Levels(), 1, "attack_infantry", mainBuildingLevel: 4, worldLevel: 1));

    /// <summary>Вузол другого ряду відкриває хоча б один рівень передумови.</summary>
    [Fact]
    public void Availability_ShouldNeedOneLevelOfEachPrerequisite()
    {
        Assert.Equal(TechnologyAvailability.PrerequisitesMissing,
            Personal.Tree.Availability(Levels(), 1, "command_infantry", 5, 1));
        Assert.Equal(TechnologyAvailability.Available,
            Personal.Tree.Availability(Levels((1, "attack_infantry", 1)), 1, "command_infantry", 5, 1));
    }

    /// <summary>Другий тір чекає, поки останній ряд першого вивчено повністю, а не просто розпочато.</summary>
    [Fact]
    public void Availability_ShouldOpenTheNextTier_OnlyAfterTheLastRowIsMaxed()
    {
        var started = Levels((1, "attack_infantry", 3), (1, "command_infantry", 1));
        var finished = Levels((1, "attack_infantry", 3), (1, "command_infantry", 2));

        Assert.Equal(TechnologyAvailability.PreviousTierIncomplete,
            Personal.Tree.Availability(started, 2, "attack_infantry", 10, 2));
        Assert.Equal(TechnologyAvailability.Available,
            Personal.Tree.Availability(finished, 2, "attack_infantry", 10, 2));
    }

    [Fact]
    public void Availability_ShouldReportAMaxedNode()
        => Assert.Equal(TechnologyAvailability.Maxed,
            Personal.Tree.Availability(Levels((1, "attack_infantry", 3)), 1, "attack_infantry", 5, 1));

    [Fact]
    public void Availability_ShouldReportAnUnknownNodeOrTier()
    {
        Assert.Equal(TechnologyAvailability.UnknownNode, Personal.Tree.Availability(Levels(), 1, "nothing", 5, 1));
        Assert.Equal(TechnologyAvailability.UnknownNode, Personal.Tree.Availability(Levels(), 3, "attack_infantry", 99, 9));
    }

    /// <summary>
    /// Бонус — рівень × бонус за рівень × шкала тіру: тір 1 дає 3 × 1%, тір 2 (шкала Battle ×2) — ще 1 × 1% × 2.
    /// Атака піхоти не змішується з атакою лучників; бонус «для всіх» додається до кожного типу.
    /// </summary>
    [Fact]
    public void Bonuses_ShouldSumLevelsTimesTheTierScale()
    {
        var bonuses = Personal.Tree.Bonuses(Levels((1, "attack_infantry", 3), (2, "attack_infantry", 1), (1, "command_infantry", 2)));

        Assert.Equal(0.05, bonuses.Of(TechnologyStat.Attack, TestKeys.Infantry), 6);
        Assert.Equal(0, bonuses.Of(TechnologyStat.Attack, TestKeys.Archer), 6);
        Assert.Equal(8, bonuses.Of(TechnologyStat.ConvoySize, TestKeys.Infantry), 6);

        var clan = Clan.Tree.Bonuses(Levels((1, "v_attack_all", 2)));

        Assert.Equal(0.07, bonuses.Plus(clan).Of(TechnologyStat.Attack, TestKeys.Infantry), 6);
        Assert.Equal(0.02, bonuses.Plus(clan).Of(TechnologyStat.Attack, TestKeys.Archer), 6);
    }

    /// <summary>Ключ вузла, якого вже немає в конфігу, бонусу не дає й нічого не ламає.</summary>
    [Fact]
    public void Bonuses_ShouldSkipNodesMissingFromTheConfig()
        => Assert.True(Personal.Tree.Bonuses(Levels((1, "removed", 5))).IsEmpty);

    /// <summary>
    /// Ряд 2, рівень 2, тір 2: 15 хв × 2 × 1,3 × 1,25 = 48,75 хв.
    /// Ціна їжі: 48,75 × 5 × опорна ратуша 10 × вага 1 = 2437,5 → 2450; золота з вагою 0,5 — 1218,75 → 1200.
    /// </summary>
    [Fact]
    public void PersonalLevel_ShouldCostTimeAndResourcesByTheFormula()
    {
        var node = Personal.Tree.Node("command_infantry")!;

        Assert.Equal(48.75, Personal.Duration(2, node, 2).TotalMinutes, 6);

        var cost = Personal.Cost(2, node, 2).ToDictionary(c => c.Resource, c => c.Amount);
        Assert.Equal(2450, cost[TestKeys.Food]);
        Assert.Equal(1200, cost[TestKeys.Gold]);
    }

    /// <summary>Ряд 2, рівень 3, тір 2: 1500 × 2 × 1,2 × 1,3² = 6084 → 6100 очок.</summary>
    [Fact]
    public void ClanLevel_ShouldCostPointsByTheFormula()
        => Assert.Equal(6100, Clan.Points(2, Clan.Tree.Node("c_coins")!, 3));

    [Fact]
    public void AttemptCost_ShouldGrowWithTheTownHall_AndRefuseUnpricedResources()
    {
        Assert.Equal(500, Clan.AttemptCost(TestKeys.Gold, 10));
        Assert.Null(Clan.AttemptCost(TestKeys.Iron, 10));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(3, 60)]
    [InlineData(25, 400)]
    public void GemOfferingPrice_ShouldGrowPerOfferingUpToTheCap(int nth, int price)
        => Assert.Equal(price, Clan.GemOfferingPrice(nth));

    /// <summary>Ваги 90 і 10: кидок 0,95 падає у ×10; бонус монет клану +50% множиться поверх.</summary>
    [Theory]
    [InlineData(0.10, 0, 100)]
    [InlineData(0.95, 0, 1000)]
    [InlineData(0.95, 0.5, 1500)]
    public void RollCoins_ShouldPickTheMultiplierByWeight(double roll, double bonus, int coins)
        => Assert.Equal(coins, Clan.RollCoins(new FixedRandom(roll), bonus));

    private sealed class FixedRandom(double value) : IRandomSource
    {
        public int Next(int maxValue) => 0;

        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble() => value;
    }
}
