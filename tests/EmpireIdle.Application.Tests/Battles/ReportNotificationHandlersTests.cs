using EmpireIdle.Application.Battles.EventHandlers;
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Battles;

/// <summary>Звіти захисту й розвідки доходять до гравця з обробників outbox — після коміту.</summary>
public class ReportNotificationHandlersTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();

    [Fact]
    public async Task DefenceReported_ShouldNotifyTheDefender()
    {
        var e = new DefenceReported(Guid.NewGuid(), Guid.NewGuid(), Won: true, "Північ", Now);

        await new DefenceReportedNotificationHandler(_notifier)
            .Handle(new DomainEventNotification<DefenceReported>(e), CancellationToken.None);

        await _notifier.Received(1).NotifyBattleFinishedAsync(e.PlayerId, e.ReportId, true, "Північ", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScoutReportFiled_ShouldNotifyTheScouter()
    {
        var e = new ScoutReportFiled(Guid.NewGuid(), Guid.NewGuid(), "Північ", ScoutOutcome.Success, Now);

        await new ScoutReportFiledNotificationHandler(_notifier)
            .Handle(new DomainEventNotification<ScoutReportFiled>(e), CancellationToken.None);

        await _notifier.Received(1).NotifyScoutReportReadyAsync(e.PlayerId, e.ReportId, "Північ", "Success",
            Arg.Any<CancellationToken>());
    }
}
