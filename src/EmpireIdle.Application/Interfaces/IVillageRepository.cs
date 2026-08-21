using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з Village aggregate.
    /// </summary>
    public interface IVillageRepository : IRepository<Village>
    {
        /// <summary>Знайти село за ідентифікатором гравця.</summary>
        Task<Village?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
        /// <summary>Знайти село за ідентифікатором гравця для читання (без трекінгу).</summary>
        Task<Village?> GetByPlayerIdReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Села, у яких є будівництва з простроченим часом завершення.</summary>
        Task<List<Village>> GetWithDueConstructionsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

    }
}
