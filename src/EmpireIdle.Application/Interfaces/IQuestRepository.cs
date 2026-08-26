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
        /// Застарілі дейліки одного гравця. Без batchSize: у гравця їх одиниці,
        /// і всі мають скинутись за один прохід — інакше частина лишиться до завтра.
        /// Фільтр по світу застосовує query-фільтр.
        /// </summary>
        Task<List<QuestProgress>> GetStaleDailyForPlayerAsync(Guid playerId, IReadOnlySet<string> questKeys,
            DateTime startedBefore, CancellationToken cancellationToken = default);

        /// <summary>
        /// Id гравців, у яких є дейліки, розпочаті до вказаної дати.
        /// Distinct: у гравця кілька дейліків, а обробляється він один раз.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetPlayerIdsWithStaleDailyAsync(
            IReadOnlySet<string> questKeys, DateTime startedBefore, int batchSize,
            CancellationToken cancellationToken = default);
    }
}
