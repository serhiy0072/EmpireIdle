
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

        /// <summary>Гравець акаунта на конкретному сервері.</summary>
        Task<Player?> GetByUserIdAsync(string userId, int serverId, CancellationToken cancellationToken = default);

        /// <summary>Усі гравці акаунта — для вибору сервера при вході.</summary>
        Task<List<Player>> GetAllByUserIdAsync(string userId, CancellationToken cancellationToken = default);

        /// <summary>Імена гравців за списком id — для топу, одним запитом.</summary>
        Task<Dictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
    }
}
