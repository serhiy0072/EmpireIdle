using EmpireIdle.Application.Marches.ReadModels;
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

        /// <summary>
        /// Ворожі марші в дорозі на села <paramref name="defenderPlayerIds"/> і на споруди
        /// клану <paramref name="clanId"/>, найближче прибуття — першим.
        /// </summary>
        Task<List<IncomingAttack>> GetIncomingAttacksAsync(IReadOnlyCollection<Guid> defenderPlayerIds, Guid? clanId,
            CancellationToken cancellationToken = default);

        /// <summary>Один ворожий марш очима захисника; null — марш уже не в дорозі або це не напад.</summary>
        Task<IncomingAttack?> GetIncomingAttackAsync(Guid marchId, CancellationToken cancellationToken = default);

        /// <summary>Додати похід.</summary>
        Task AddAsync(March march, CancellationToken cancellationToken = default);
    }
}
