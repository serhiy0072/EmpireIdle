
using EmpireIdle.Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з Player entity.
    /// </summary>
    public interface IPlayerRepository : IRepository<Player>
    {
        /// <summary>Знайти гравця за email.</summary>
        Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}
