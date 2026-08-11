using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій походів.</summary>
    public interface IMarchRepository
    {
        /// <summary>Активні походи гарнізону (у дорозі або на зворотному шляху).</summary>
        Task<List<March>> GetActiveByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default);

        /// <summary>Походи, чий час прибуття настав.</summary>
        Task<List<March>> GetDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Додати похід.</summary>
        Task AddAsync(March march, CancellationToken cancellationToken = default);
    }
}