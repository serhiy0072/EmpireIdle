using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій звітів про бої.</summary>
    public interface IBattleReportRepository
    {
        /// <summary>Останні звіти гравця (найновіші першими).</summary>
        Task<List<BattleReport>> GetByPlayerAsync(Guid playerId, int take, CancellationToken cancellationToken = default);

        /// <summary>Звіт за ідентифікатором.</summary>
        Task<BattleReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Додати звіт.</summary>
        Task AddAsync(BattleReport report, CancellationToken cancellationToken = default);
    }
}