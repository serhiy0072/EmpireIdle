using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Villages.EventHandlers
{
    /// <summary>Реагує на BuildingCollected: шле гравцю real-time сповіщення про збір.</summary>
    public sealed class BuildingCollectedNotificationHandler : INotificationHandler<DomainEventNotification<BuildingCollected>>
    {
        private readonly IGameNotifier _notifier;

        public BuildingCollectedNotificationHandler(IGameNotifier notifier) => _notifier = notifier;
        

        public async Task Handle(DomainEventNotification<BuildingCollected> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            await _notifier.NotifyBuildingCollectedAsync(e.PlayerId, e.BuildingId, e.ResourceType, e.Amount, e.NewVillageAmount, cancellationToken);
        }
    }
}
