using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій бойової сили гравців.</summary>
    public interface IPlayerPowerRepository
    {
        /// <summary>
        /// Сила гравця; null — перерахунку ще не було.
        /// З трекінгом: перерахунок мутує знайдений рядок.
        /// </summary>
        Task<PlayerPower?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Сила всіх гравців світу — для годинного перерахунку рейтингу.</summary>
        Task<List<PlayerPower>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Створює рядок сили. Викликається один раз на гравця, при першій
        /// події, що змінює армію — унікальний індекс на PlayerId не дасть другий.
        /// </summary>
        Task AddAsync(PlayerPower power, CancellationToken cancellationToken = default);

        /// <summary>Сила лише вказаних гравців — для топу, одним запитом.</summary>
        Task<Dictionary<Guid, double>> GetTotalPowerAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
    }
}
