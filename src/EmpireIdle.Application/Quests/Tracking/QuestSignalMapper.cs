using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Application.Quests.Tracking
{
    /// <summary>Мапер для конкретного типу події.</summary>
    public abstract class QuestSignalMapper<TEvent> : IQuestSignalMapper where TEvent : IDomainEvent
    {
        /// <inheritdoc/>
        public Type EventType => typeof(TEvent);

        /// <inheritdoc/>
        public Task<QuestSignal?> MapAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
            => Map((TEvent)domainEvent, cancellationToken);

        /// <summary>Типізоване відображення події в сигнал.</summary>
        protected abstract Task<QuestSignal?> Map(TEvent domainEvent, CancellationToken cancellationToken);
    }
}
