using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Battles.EventHandlers
{
    /// <summary>Сповіщає захисника про бій — уже після того, як звіт закомічено.</summary>
    public sealed class DefenceReportedNotificationHandler : INotificationHandler<DomainEventNotification<DefenceReported>>
    {
        private readonly IGameNotifier _notifier;

        public DefenceReportedNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<DefenceReported> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return _notifier.NotifyBattleFinishedAsync(e.PlayerId, e.ReportId, e.Won, e.AttackerName, cancellationToken);
        }
    }

    /// <summary>Сповіщає гравця, що звіт розвідки готовий.</summary>
    public sealed class ScoutReportFiledNotificationHandler : INotificationHandler<DomainEventNotification<ScoutReportFiled>>
    {
        private readonly IGameNotifier _notifier;

        public ScoutReportFiledNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<ScoutReportFiled> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return _notifier.NotifyScoutReportReadyAsync(e.PlayerId, e.ReportId, e.TargetName, e.Outcome.ToString(),
                cancellationToken);
        }
    }
}
