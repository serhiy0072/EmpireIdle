
using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Villages.EventHandlers
{
    public sealed class BuildingUpgradeCompletedNotificationHandler : INotificationHandler<DomainEventNotification<BuildingUpgradeCompleted>>
    {
        private readonly IGameNotifier _notifier;

        public BuildingUpgradeCompletedNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<BuildingUpgradeCompleted> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return _notifier.NotifyUpgradeCompletedAsync(e.PlayerId, e.BuildingId, e.NewLevel.Value, cancellationToken);
        }
    }
}
