

using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Villages.EventHandlers
{
    public sealed class BuildingUpgradeStartedNotificationHandler : INotificationHandler<DomainEventNotification<BuildingUpgradeStarted>>
    {
        private readonly IGameNotifier _notifier;
        public BuildingUpgradeStartedNotificationHandler(IGameNotifier notifier) => _notifier = notifier;

        public Task Handle(DomainEventNotification<BuildingUpgradeStarted> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return _notifier.NotifyUpgradeStartedAsync(e.PlayerId, e.BuildingId, e.ConstructionCompletesAt, cancellationToken);
        }
    }
}
