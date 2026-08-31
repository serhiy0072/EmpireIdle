using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій серверних квестів і внесків у них.</summary>
    public interface IServerQuestRepository
    {
        /// <summary>Спільний прогрес квесту в поточному світі; null — ще не створений.</summary>
        Task<ServerQuestProgress?> GetProgressAsync(string questKey, CancellationToken cancellationToken = default);

        /// <summary>Усі незавершені квести світу — для джоба підрахунку.</summary>
        Task<List<ServerQuestProgress>> GetActiveAsync(CancellationToken cancellationToken = default);

        Task AddProgressAsync(ServerQuestProgress progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Внесок гравця; null — ще не вносив.
        /// З трекінгом: Add мутує знайдений рядок.
        /// </summary>
        Task<ServerQuestContribution?> GetContributionAsync(string questKey, Guid playerId,
            CancellationToken cancellationToken = default);

        Task AddContributionAsync(ServerQuestContribution contribution, CancellationToken cancellationToken = default);

        /// <summary>Сума всіх внесків у квест — джоб рахує її в базі, не в пам'яті.</summary>
        Task<long> SumContributionsAsync(string questKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Внески, впорядковані за рангом: більший раніше, нічия — за часом.
        /// Саме цей порядок визначає ярус нагороди.
        /// </summary>
        Task<List<ServerQuestContribution>> GetRankedAsync(string questKey,
            CancellationToken cancellationToken = default);
    }
}
