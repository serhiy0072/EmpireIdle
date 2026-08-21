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
        private readonly ILogger<QuestProgressHandler<TEvent>> _logger;

        public QuestProgressHandler(QuestSignalResolver resolver, QuestProgressTracker tracker,
            ILogger<QuestProgressHandler<TEvent>> logger)
        {
            _resolver = resolver;
            _tracker = tracker;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<TEvent> notification, CancellationToken cancellationToken)
        {
            var signal = await _resolver.ResolveAsync(notification.DomainEvent, cancellationToken);

            // Подія не бере участі в квестах — не помилка
            if (signal is null)
                return;

            await _tracker.TrackAsync(signal, DateTime.UtcNow, cancellationToken);
        }
    }
}
