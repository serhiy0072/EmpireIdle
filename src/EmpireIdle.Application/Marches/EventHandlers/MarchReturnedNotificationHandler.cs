using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.EventHandlers
{
    /// <summary>
    /// Надсилає гравцю realtime-сповіщення, що армія вдома.
    ///
    /// Подія знає лише гарнізон: марш не тримає id гравця. Власника
    /// знаходимо через село — це два читання на одне повернення, і вони
    /// йдуть після коміту транзакції (outbox), тож на сам марш не впливають.
    /// </summary>
    public sealed class MarchReturnedNotificationHandler : INotificationHandler<DomainEventNotification<MarchReturned>>
    {
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IGameNotifier _notifier;
        private readonly ILogger<MarchReturnedNotificationHandler> _logger;

        public MarchReturnedNotificationHandler(
            IGarrisonRepository garrisonRepository,
            IVillageRepository villageRepository,
            IGameNotifier notifier,
            ILogger<MarchReturnedNotificationHandler> logger)
        {
            _garrisonRepository = garrisonRepository;
            _villageRepository = villageRepository;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<MarchReturned> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var garrison = await _garrisonRepository.GetByIdAsync(e.GarrisonId, cancellationToken);
            var village = garrison is null
                ? null
                : await _villageRepository.GetByIdAsync(garrison.VillageId, cancellationToken);

            // Село могли видалити між поверненням і обробкою outbox — сповіщати нікого
            if (village is null)
            {
                _logger.LogWarning("March {MarchId} returned to garrison {GarrisonId}, but its village was not found.",
                    e.MarchId, e.GarrisonId);
                return;
            }

            await _notifier.NotifyMarchReturnedAsync(village.PlayerId, e.MarchId, cancellationToken);
        }
    }
}
