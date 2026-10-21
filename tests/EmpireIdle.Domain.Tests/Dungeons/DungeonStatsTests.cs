using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Tests.Dungeons;

/// <summary>Стати героя у відсотках стають частками для рушія; перезарядка лишається цілими ходами.</summary>
public class DungeonStatsTests
{
    [Fact]
    public void From_ShouldTurnPercentsIntoShares()
    {
        var stats = DungeonStats.From(new Dictionary<string, double>
        {
            ["CritChance"] = 4, ["CritPower"] = 19, ["SkillDamage"] = 23, ["Lifesteal"] = 6, ["DamageReduction"] = 3,
            ["BlockChance"] = 4, ["BlockPower"] = 16, ["Ward"] = 16, ["CooldownTurns"] = 1,
        });

        Assert.Equal(0.04, stats[DungeonStat.CritChance], 6);
        Assert.Equal(0.19, stats[DungeonStat.CritPower], 6);
        Assert.Equal(0.23, stats[DungeonStat.SkillDamage], 6);
        Assert.Equal(0.06, stats[DungeonStat.Lifesteal], 6);
        Assert.Equal(0.03, stats[DungeonStat.DamageReduction], 6);
        Assert.Equal(0.04, stats[DungeonStat.BlockChance], 6);
        Assert.Equal(0.16, stats[DungeonStat.BlockPower], 6);
        Assert.Equal(0.16, stats[DungeonStat.Ward], 6);
        Assert.Equal(1, stats[DungeonStat.CooldownReduction], 6);
    }

    /// <summary>Армійські й базові стати в данж не потрапляють.</summary>
    [Fact]
    public void From_ShouldIgnoreStatsTheDungeonDoesNotKnow()
        => Assert.Empty(DungeonStats.From(new Dictionary<string, double> { ["Attack"] = 100, ["TroopsAttack"] = 5, ["MarchSpeed"] = 10 }));
}
