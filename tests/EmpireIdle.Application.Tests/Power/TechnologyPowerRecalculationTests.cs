using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Power.Commands;
using EmpireIdle.Application.Power.Tracking;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Events;
using EmpireIdle.TestKit;
using MediatR;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Power;

/// <summary>Хто перераховує силу технологій: подія каже «перерахуй» кожному, чия сила змінилась.</summary>
public class TechnologyPowerRecalculationTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();

    [Fact]
    public async Task ResearchCompleted_ShouldRecalculateThePlayer()
    {
        await new RecalculatePowerOnResearchCompleted(_mediator).Handle(
            new DomainEventNotification<ResearchCompleted>(new ResearchCompleted(PlayerId, "1:node", 1, Now)), CancellationToken.None);

        await _mediator.Received(1).Send(new RecalculatePlayerPowerCommand(PlayerId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MembershipChange_ShouldRecalculateThatPlayerOnly()
    {
        await new RecalculatePowerOnClanMembershipChanged(_mediator).Handle(
            new DomainEventNotification<ClanMembershipChanged>(new ClanMembershipChanged(PlayerId, Guid.NewGuid(), Now)), CancellationToken.None);

        await _mediator.Received(1).Send(new RecalculatePlayerPowerCommand(PlayerId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClanLevels_ShouldRecalculateEveryMember()
    {
        var member = Guid.NewGuid();
        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", PlayerId, Now);
        clan.Join(member, capacity: 10, Now);
        _clans.GetByIdAsync(clan.Id, Arg.Any<CancellationToken>()).Returns(clan);

        await new RecalculatePowerOnClanTechnologyLevelsCompleted(_clans, _mediator).Handle(
            new DomainEventNotification<ClanTechnologyLevelsCompleted>(new ClanTechnologyLevelsCompleted(clan.Id, "1:node", 1, Now)), CancellationToken.None);

        await _mediator.Received(1).Send(new RecalculatePlayerPowerCommand(PlayerId), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(new RecalculatePlayerPowerCommand(member), Arg.Any<CancellationToken>());
    }

    /// <summary>Клан розпустили, поки подія чекала в outbox: нікого перераховувати, і це не помилка.</summary>
    [Fact]
    public async Task ClanLevels_ShouldDoNothing_WhenTheClanIsGone()
    {
        _clans.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Clan?)null);

        await new RecalculatePowerOnClanTechnologyLevelsCompleted(_clans, _mediator).Handle(
            new DomainEventNotification<ClanTechnologyLevelsCompleted>(new ClanTechnologyLevelsCompleted(Guid.NewGuid(), "1:node", 1, Now)), CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<RecalculatePlayerPowerCommand>(), Arg.Any<CancellationToken>());
    }
}
