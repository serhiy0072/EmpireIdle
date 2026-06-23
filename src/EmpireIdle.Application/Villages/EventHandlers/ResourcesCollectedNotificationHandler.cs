using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Villages.EventHandlers
{
    /// <summary>
    /// Реагує на ResourcesCollected: надсилає гравцю real-time оновлення ресурсів села.
    /// </summary>
    public sealed class ResourcesCollectedNotificationHandler : INotificationHandler<DomainEventNotification<ResourcesCollected>>
    {
        private readonly IGameNotifier _notifier;

        public ResourcesCollectedNotificationHandler(IGameNotifier notifier)
        {
            _notifier = notifier;
        }

        public async Task Handle(DomainEventNotification<ResourcesCollected> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var resources = e.Resources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
            await _notifier.NotifyResourcesUpdatedAsync(e.PlayerId, resources, cancellationToken);
        }
    }
}
