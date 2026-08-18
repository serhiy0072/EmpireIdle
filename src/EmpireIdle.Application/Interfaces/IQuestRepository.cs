using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій прогресу квестів.</summary>
    public interface IQuestRepository
    {
        /// <summary>Прогрес конкретного квесту; null — гравець його ще не починав.</summary>
        Task<QuestProgress?> GetAsync(Guid playerId, string questKey, CancellationToken cancellationToken = default);

        /// <summary>Увесь прогрес гравця — для списку квестів і перевірки пререквізитів.</summary>
        Task<List<QuestProgress>> GetAllAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task AddAsync(QuestProgress progress, CancellationToken cancellationToken = default);
    }
}
