using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Звіти розвідки гравця.</summary>
    public interface IScoutReportRepository
    {
        Task AddAsync(ScoutReport report, CancellationToken cancellationToken = default);

        /// <summary>Найсвіжіші звіти гравця разом із ресурсами, новіші першими.</summary>
        Task<List<ScoutReport>> GetRecentAsync(Guid playerId, int take, CancellationToken cancellationToken = default);
    }
}
