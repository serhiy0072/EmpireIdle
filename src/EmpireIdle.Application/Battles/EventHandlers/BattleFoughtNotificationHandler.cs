using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Battles.EventHandlers
{
    /// <summary>Надсилає гравцю realtime-сповіщення про результат бою.</summary>
    public sealed class BattleFoughtNotificationHandler : INotificationHandler<DomainEventNotification<BattleFought>>
    {
        private readonly IGameNotifier _notifier;

        public BattleFoughtNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<BattleFought> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return _notifier.NotifyBattleFinishedAsync(e.PlayerId, e.ReportId, e.Won, e.TargetName, cancellationToken);
        }
    }
}