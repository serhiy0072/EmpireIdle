using EmpireIdle.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з Village aggregate.
    /// </summary>
    public interface IVillageRepository : IRepository<Village>
    {
        /// <summary>Знайти село за ідентифікатором гравця.</summary>
        Task<Village?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);
    }
}
