using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Очки підношень: рівні закриваються самі, надлишок не губиться, зміна цілі нічого не стирає.</summary>
public class ClanResearchTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ClanTechnologies Technologies = new(new GameConfigBuilder().WithTechnologies().Build().Technologies.Clan);
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");
    private static readonly TechnologyKey Mint = new(1, "c_coins");

    private static int PointsFor(int level) => Technologies.Points(1, Technologies.Tree.Node("v_attack_all")!, level);

    private static ClanResearch Research()
    {
        var research = new ClanResearch(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        research.SelectTarget(Standard, Now);

        return research;
    }

    [Fact]
    public void AddPoints_ShouldRefuse_WhenNothingIsSelected()
    {
        var research = new ClanResearch(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        var error = Assert.Throws<RequirementNotMetException>(() => research.AddPoints(10, Technologies, Now));

        Assert.Equal(RefusalReasons.ClanTechNotSelected.Key, error.Reason);
    }

    [Fact]
    public void AddPoints_ShouldOnlyAccumulate_BelowTheLevelPrice()
    {
        var research = Research();

        var completed = research.AddPoints(PointsFor(1) - 1, Technologies, Now);

        Assert.Empty(completed);
        Assert.Equal(0, research.LevelOf(Standard));
        Assert.Equal(PointsFor(1) - 1, research.ProgressOf(Standard));
    }

    [Fact]
    public void AddPoints_ShouldCloseTheLevel_KeepTheTarget_AndCarryTheSurplus()
    {
        var research = Research();

        var completed = research.AddPoints(PointsFor(1) + 7, Technologies, Now);

        Assert.Equal((Standard, 1), Assert.Single(completed));
        Assert.Equal(1, research.LevelOf(Standard));
        Assert.Equal(7, research.ProgressOf(Standard));
        Assert.Equal(Standard.ToString(), research.TargetKey);
    }

    [Fact]
    public void AddPoints_ShouldClearTheTarget_WhenTheNodeIsFinished()
    {
        var research = Research();
        var all = PointsFor(1) + PointsFor(2) + PointsFor(3);

        var completed = research.AddPoints(all + 50, Technologies, Now);

        Assert.Equal(3, completed.Count);
        Assert.Equal(3, research.LevelOf(Standard));
        Assert.Null(research.TargetKey);
        Assert.Equal(0, research.ProgressOf(Standard));
    }

    [Fact]
    public void SelectTarget_ShouldKeepTheProgressOfTheOldTarget()
    {
        var research = Research();
        research.AddPoints(100, Technologies, Now);

        research.SelectTarget(Mint, Now);
        research.AddPoints(30, Technologies, Now);
        research.SelectTarget(Standard, Now);

        Assert.Equal(100, research.ProgressOf(Standard));
        Assert.Equal(30, research.ProgressOf(Mint));
    }
}
