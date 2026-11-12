using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Підношення йдуть прямо у вибраний вузол: частковий поступ, кілька рівнів, залишок після стелі.</summary>
public class ClanResearchTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);
    private static readonly ClanTechnologies Technologies = new(new GameConfigBuilder().WithTechnologies().Build().Technologies.Clan);
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");
    private static readonly TechnologyKey Mint = new(1, "c_coins");

    private static int PointsFor(int level) => Technologies.Points(1, Technologies.Tree.Node("v_attack_all")!, level);

    private static ClanResearch Research() => new(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

    [Fact]
    public void Invest_ShouldKeepPartialProgress_AndCloseTheLevelLater()
    {
        var research = Research();

        Assert.Empty(research.Invest(Standard, PointsFor(1) - 5, Technologies, Now));
        Assert.Equal(PointsFor(1) - 5, research.ProgressOf(Standard));

        var completed = research.Invest(Standard, 5, Technologies, Now);

        Assert.Equal((Standard, 1), Assert.Single(completed));
        Assert.Equal(0, research.ProgressOf(Standard));
    }

    [Fact]
    public void Invest_ShouldCloseSeveralLevels_AndCarryTheRemainderToTheNext()
    {
        var research = Research();

        var completed = research.Invest(Standard, PointsFor(1) + PointsFor(2) + 7, Technologies, Now);

        Assert.Equal(2, completed.Count);
        Assert.Equal(2, research.LevelOf(Standard));
        Assert.Equal(7, research.ProgressOf(Standard));
    }

    /// <summary>Понад останній рівень вкладати нікуди: залишок пропадає, а поступу на вивченому вузлі немає.</summary>
    [Fact]
    public void Invest_ShouldDropTheSurplus_WhenTheNodeIsFinished()
    {
        var research = Research();

        var completed = research.Invest(Standard, PointsFor(1) + PointsFor(2) + PointsFor(3) + 50, Technologies, Now);

        Assert.Equal(3, completed.Count);
        Assert.Equal(3, research.LevelOf(Standard));
        Assert.Equal(0, research.ProgressOf(Standard));
    }

    [Fact]
    public void Invest_ShouldKeepEachNodesProgressApart()
    {
        var research = Research();

        research.Invest(Standard, 100, Technologies, Now);
        research.Invest(Mint, 30, Technologies, Now);

        Assert.Equal(100, research.ProgressOf(Standard));
        Assert.Equal(30, research.ProgressOf(Mint));
    }

    [Fact]
    public void Mark_ShouldReplaceThePreviousMark_AndClearWithNull()
    {
        var research = Research();

        research.Mark(Standard, Now);
        research.Mark(Mint, Now);

        Assert.True(research.IsMarked(Mint));
        Assert.False(research.IsMarked(Standard));

        research.Mark(null, Now);

        Assert.Null(research.MarkedTechnology);
    }

    /// <summary>Вивчений вузол більше не приймає підношень: мітка з нього знімається сама.</summary>
    [Fact]
    public void Invest_ShouldClearTheMark_WhenTheMarkedNodeIsFinished()
    {
        var research = Research();
        research.Mark(Standard, Now);

        research.Invest(Standard, PointsFor(1), Technologies, Now);
        Assert.True(research.IsMarked(Standard));

        research.Invest(Standard, PointsFor(2) + PointsFor(3), Technologies, Now);
        Assert.Null(research.MarkedTechnology);
    }

    /// <summary>Вивчення іншого вузла чужої мітки не чіпає.</summary>
    [Fact]
    public void Invest_ShouldKeepTheMark_WhenAnotherNodeIsFinished()
    {
        var research = Research();
        research.Mark(Mint, Now);

        research.Invest(Standard, PointsFor(1) + PointsFor(2) + PointsFor(3), Technologies, Now);

        Assert.True(research.IsMarked(Mint));
    }

    [Fact]
    public void Invest_ShouldRaiseOnce_OnlyWhenALevelCloses()
    {
        var research = Research();

        research.Invest(Standard, PointsFor(1) - 1, Technologies, Now);
        Assert.Empty(research.DomainEvents);

        research.Invest(Standard, PointsFor(2) + 1, Technologies, Now);
        Assert.IsType<ClanTechnologyLevelsCompleted>(Assert.Single(research.DomainEvents));
    }
}
