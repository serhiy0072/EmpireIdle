
using EmpireIdle.Domain.Entities;

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
