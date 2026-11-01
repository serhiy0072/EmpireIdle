using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Особисті технології гравців (рішення 28.10.2026).</summary>
    public interface IPlayerResearchRepository
    {
        /// <summary>Технології гравця для зміни або null, якщо він ще нічого не досліджував.</summary>
        Task<PlayerResearch?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Технології гравця лише для читання.</summary>
        Task<PlayerResearch?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        Task<PlayerResearch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Ключі активних досліджень за id записів; записи без активного дослідження відсутні.</summary>
        Task<Dictionary<Guid, string>> GetActiveKeysAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

        Task AddAsync(PlayerResearch research, CancellationToken cancellationToken = default);

        /// <summary>Id записів поточного світу, чиє дослідження дозріло, — для сканера таймерів.</summary>
        Task<IReadOnlyList<Guid>> GetIdsWithDueResearchAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);
    }
}
