using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій запитів на кланову допомогу.</summary>
    public interface IClanHelpRepository
    {
        /// <summary>Запит зі списком тих, хто вже допоміг.</summary>
        Task<ClanHelpRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);

        /// <summary>Активні запити клану — те, що бачить список допомоги.</summary>
        Task<List<ClanHelpRequest>> GetActiveByClanAsync(Guid clanId, DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>Чи є вже запит на цю ціль — повторний засмітив би список.</summary>
        Task<bool> ExistsForTargetAsync(Guid targetId, CancellationToken cancellationToken = default);

        Task AddAsync(ClanHelpRequest request, CancellationToken cancellationToken = default);

        /// <summary>Прибирає прострочені запити (фонове очищення).</summary>
        Task<int> RemoveExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
