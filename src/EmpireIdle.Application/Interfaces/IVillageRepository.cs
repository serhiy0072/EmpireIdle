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

        /// <summary>Id сіл, де є завершені будівництва. Без сутностей: обробка йде в іншому scope.</summary>
        Task<IReadOnlyList<Guid>> GetIdsWithDueConstructionsAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Медіана рівня головної будівлі серед сіл поточного світу.
        /// Медіана, а не середнє: мертві акаунти з ратушею 1 рівня
        /// тягнули б середнє вниз і блокували ріст живого світу.
        /// </summary>
        Task<int> GetMedianMainBuildingLevelAsync(string mainBuildingKey, CancellationToken cancellationToken = default);

        /// <summary>Скільки сіл у поточному світі.</summary>
        Task<int> CountAsync(CancellationToken cancellationToken = default);

        /// <summary>Усі села світу з будівлями — для годинного перерахунку рейтингу.</summary>
        Task<List<Village>> GetAllWithBuildingsAsync(CancellationToken cancellationToken = default);
    }
}
