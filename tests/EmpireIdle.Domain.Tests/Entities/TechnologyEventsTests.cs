using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Події технологій: на них стоять квести й перерахунок сили.</summary>
public class TechnologyEventsTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly TechnologyKey Key = new(1, "attack_infantry");

    private static PlayerResearch Researching(TimeSpan duration)
    {
        var research = new PlayerResearch(Guid.NewGuid(), PlayerId, 1, Now);
        research.Start(Key, duration, Now);

        return research;
    }

    [Fact]
    public void CompleteDue_ShouldRaiseResearchCompleted_WithTheNewLevel()
    {
        var research = Researching(TimeSpan.FromHours(1));

        research.CompleteDue(Now.AddHours(1));

        var raised = Assert.IsType<ResearchCompleted>(Assert.Single(research.DomainEvents));
        Assert.Equal(new ResearchCompleted(PlayerId, Key.ToString(), 1, Now.AddHours(1)), raised);
    }

    [Fact]
    public void CompleteDue_ShouldStayQuiet_WhileTheTimerRuns()
    {
        var research = Researching(TimeSpan.FromHours(1));

        Assert.Null(research.CompleteDue(Now.AddMinutes(59)));
        Assert.Empty(research.DomainEvents);
    }

    /// <summary>Сканер таймерів і дія гравця можуть застати те саме дослідження: другий виклик нічого не рахує.</summary>
    [Fact]
    public void CompleteDue_ShouldRaiseOnce_WhenCalledTwice()
    {
        var research = Researching(TimeSpan.Zero);

        research.CompleteDue(Now);
        research.CompleteDue(Now);

        Assert.Single(research.DomainEvents);
    }

    [Fact]
    public void ContributionAdd_ShouldRaiseClanOfferingMade()
    {
        var clanId = Guid.NewGuid();
        var contribution = new ClanContribution(Guid.NewGuid(), clanId, PlayerId, 1, Now);

        contribution.Add(10, Now);

        var raised = Assert.IsType<ClanOfferingMade>(Assert.Single(contribution.DomainEvents));
        Assert.Equal(new ClanOfferingMade(PlayerId, clanId, 10, Now), raised);
    }
}
