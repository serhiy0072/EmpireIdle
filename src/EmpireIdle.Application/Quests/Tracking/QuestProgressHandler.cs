using EmpireIdle.Application.Common.Events;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Tracking
{
    /// <summary>
    /// Підписник на будь-яку доменну подію. Один узагальнений клас замість
    /// окремого хендлера на кожен тип — закриті типи реєструються рефлексією.
    /// </summary>
    public sealed class QuestProgressHandler<TEvent> : INotificationHandler<DomainEventNotification<TEvent>>
        where TEvent : IDomainEvent
    {
        private readonly QuestSignalResolver _resolver;
        private readonly QuestProgressTracker _tracker;
        private readonly TimeProvider _timeProvider;

        public QuestProgressHandler(QuestSignalResolver resolver, QuestProgressTracker tracker, TimeProvider timeProvider)
        {
            _resolver = resolver;
            _tracker = tracker;
            _timeProvider = timeProvider;
        }

        public async Task Handle(DomainEventNotification<TEvent> notification, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var signal = await _resolver.ResolveAsync(notification.DomainEvent, cancellationToken);

            // Подія не бере участі в квестах — не помилка
            if (signal is null)
                return;

            // Усі три виміри: особисті, серверні й кланові квести. Лише особисті губили
            // внески серверних і прогрес кланових — outbox позначав подію обробленою назавжди
            await _tracker.TrackAsync(signal, now, cancellationToken);
        }
    }
}
