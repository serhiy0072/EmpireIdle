using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій прогресу квестів.</summary>
    public interface IQuestRepository : IRepository<QuestProgress>
    {
        /// <summary>Прогрес конкретного квесту; null — гравець його ще не починав.</summary>
        Task<QuestProgress?> GetAsync(Guid playerId, string questKey, CancellationToken cancellationToken = default);

        /// <summary>Увесь прогрес гравця — для списку квестів і перевірки пререквізитів.</summary>
        Task<List<QuestProgress>> GetAllAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Прогрес гравця по вказаних квестах — для трекера, замість завантаження всього.</summary>
        Task<List<QuestProgress>> GetByKeysAsync(Guid playerId, IReadOnlySet<string> questKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// Застарілі дейліки поточного світу, не більше <paramref name="batchSize"/>.
        /// Фільтр по світу застосовує query-фільтр.
        /// </summary>
        Task<List<QuestProgress>> GetStaleDailyAsync(IReadOnlySet<string> questKeys, DateTime startedBefore,
            int batchSize, CancellationToken cancellationToken = default);
    }
}
