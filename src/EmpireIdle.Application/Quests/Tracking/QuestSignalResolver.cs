using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Tracking
{
    /// <summary>Добирає мапер за типом події.</summary>
    public class QuestSignalResolver
    {
        private readonly Dictionary<Type, IQuestSignalMapper> _mappers;

        public QuestSignalResolver(IEnumerable<IQuestSignalMapper> mappers)
            => _mappers = mappers.ToDictionary(m => m.EventType);

        /// <summary>Сигнал або null, якщо подія не бере участі в квестах.</summary>
        public Task<QuestSignal?> ResolveAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
            => _mappers.TryGetValue(domainEvent.GetType(), out var mapper)
                ? mapper.MapAsync(domainEvent, cancellationToken)
                : Task.FromResult<QuestSignal?>(null);
    }
}
