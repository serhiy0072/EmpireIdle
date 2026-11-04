using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Запас очок клану: підношення наповнюють, офіцер розподіляє, зайве не губиться.</summary>
public class ClanResearchTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ClanTechnologies Technologies = new(new GameConfigBuilder().WithTechnologies().Build().Technologies.Clan);
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");
    private static readonly TechnologyKey Mint = new(1, "c_coins");

    private static int PointsFor(int level) => Technologies.Points(1, Technologies.Tree.Node("v_attack_all")!, level);

    private static ClanResearch Research(int points)
    {
        var research = new ClanResearch(Guid.NewGuid(), Guid.NewGuid(), 1, Now);
        research.AddPoints(points, Now);

        return research;
    }

    [Fact]
    public void AddPoints_ShouldOnlyFillThePool()
    {
        var research = Research(30);
        research.AddPoints(20, Now);

        Assert.Equal(50, research.UnallocatedPoints);
        Assert.Empty(research.Levels);
        Assert.Empty(research.Progress);
    }

    [Fact]
    public void Allocate_ShouldRefuse_MoreThanThePoolHolds()
    {
        var research = Research(10);

        var error = Assert.Throws<RequirementNotMetException>(() => research.Allocate(Standard, 11, Technologies, Now));

        Assert.Equal(RefusalReasons.ClanTechNotEnoughPoints.Key, error.Reason);
        Assert.Equal(10, research.UnallocatedPoints);
    }

    [Fact]
    public void Allocate_ShouldInvestPartially_AndContinueLater()
    {
        var research = Research(PointsFor(1));

        Assert.Empty(research.Allocate(Standard, PointsFor(1) - 5, Technologies, Now));
        Assert.Equal(PointsFor(1) - 5, research.ProgressOf(Standard));
        Assert.Equal(5, research.UnallocatedPoints);

        var completed = research.Allocate(Standard, 5, Technologies, Now);

        Assert.Equal((Standard, 1), Assert.Single(completed));
        Assert.Equal(0, research.ProgressOf(Standard));
        Assert.Equal(0, research.UnallocatedPoints);
    }

    [Fact]
    public void Allocate_ShouldCloseSeveralLevels_AndCarryTheRemainderToTheNext()
    {
        var research = Research(PointsFor(1) + PointsFor(2) + 7);

        var completed = research.Allocate(Standard, PointsFor(1) + PointsFor(2) + 7, Technologies, Now);

        Assert.Equal(2, completed.Count);
        Assert.Equal(2, research.LevelOf(Standard));
        Assert.Equal(7, research.ProgressOf(Standard));
        Assert.Equal(0, research.UnallocatedPoints);
    }

    /// <summary>Вузол бере лише потрібне: надлишок понад останній рівень лишається в запасі, а не згорає.</summary>
    [Fact]
    public void Allocate_ShouldLeaveTheSurplusInThePool_WhenTheNodeIsFinished()
    {
        var all = PointsFor(1) + PointsFor(2) + PointsFor(3);
        var research = Research(all + 50);

        var completed = research.Allocate(Standard, all + 50, Technologies, Now);

        Assert.Equal(3, completed.Count);
        Assert.Equal(3, research.LevelOf(Standard));
        Assert.Equal(50, research.UnallocatedPoints);
        Assert.Equal(0, research.ProgressOf(Standard));
    }

    [Fact]
    public void Allocate_ShouldKeepEachNodesProgressApart()
    {
        var research = Research(130);

        research.Allocate(Standard, 100, Technologies, Now);
        research.Allocate(Mint, 30, Technologies, Now);

        Assert.Equal(100, research.ProgressOf(Standard));
        Assert.Equal(30, research.ProgressOf(Mint));
    }
}
