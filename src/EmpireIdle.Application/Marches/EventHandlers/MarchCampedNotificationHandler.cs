using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Marches.EventHandlers
{
    /// <summary>
    /// Власнику — сповіщення, що його атака стала табором. Як і в поверненні,
    /// марш не тримає id гравця: власника знаходимо через гарнізон і село.
    /// </summary>
    public sealed class MarchCampedNotificationHandler : INotificationHandler<DomainEventNotification<MarchCamped>>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGameNotifier _notifier;

        public MarchCampedNotificationHandler(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IGameNotifier notifier)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _notifier = notifier;
        }

        public async Task Handle(DomainEventNotification<MarchCamped> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var garrison = await _garrisonRepository.GetByIdAsync(e.GarrisonId, cancellationToken);
            var village = garrison is null
                ? null
                : await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken);

            if (village is not null)
                await _notifier.NotifyMarchCampedAsync(village.PlayerId, e.MarchId, e.X, e.Y, cancellationToken);
        }
    }
}
