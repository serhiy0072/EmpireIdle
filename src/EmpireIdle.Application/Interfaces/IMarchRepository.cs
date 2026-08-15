using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій походів.</summary>
    public interface IMarchRepository
    {
        /// <summary>Активні походи гарнізону (у дорозі або на зворотному шляху).</summary>
        Task<List<March>> GetActiveByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default);

        /// <summary>Походи, чий час прибуття настав (не більше <paramref name="batchSize"/>).</summary>
        Task<List<March>> GetDueAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>Похід за ідентифікатором (із загонами).</summary>
        Task<March?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Додати похід.</summary>
        Task AddAsync(March march, CancellationToken cancellationToken = default);
    }
}
