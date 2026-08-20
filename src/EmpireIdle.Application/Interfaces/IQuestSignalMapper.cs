using EmpireIdle.Application.Quests.Tracking;
using EmpireIdle.Domain.Events;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Переводить одну доменну подію в сигнал квестів.
    /// Реалізація на подію: додати подію означає додати клас, а не правити switch.
    /// </summary>
    public interface IQuestSignalMapper
    {
        /// <summary>Тип події, який обробляє мапер.</summary>
        Type EventType { get; }

        /// <summary>Сигнал або null, якщо подія не дає прогресу.</summary>
        Task<QuestSignal?> MapAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
    }
}
