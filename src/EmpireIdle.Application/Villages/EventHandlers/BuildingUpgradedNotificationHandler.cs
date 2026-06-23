using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Villages.EventHandlers
{
    /// <summary>
    /// Реагує на BuildingUpgraded: надсилає гравцю real-time сповіщення про апгрейд.
    /// </summary>
    public sealed class BuildingUpgradedNotificationHandler: INotificationHandler<DomainEventNotification<BuildingUpgraded>>
    {
        private readonly IGameNotifier _notifier;

        public BuildingUpgradedNotificationHandler(IGameNotifier notifier)
        {
            _notifier = notifier;
        }

        public async Task Handle(DomainEventNotification<BuildingUpgraded> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            await _notifier.NotifyBuildingUpgradedAsync(e.PlayerId, e.BuildingId, e.NewLevel.Value, cancellationToken);
        }
    }
}
