using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій звітів про бої (EF Core).</summary>
    public class BattleReportRepository : IBattleReportRepository
    {
        private readonly AppDbContext _context;

        public BattleReportRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<BattleReport>> GetByPlayerAsync(Guid playerId, int take, CancellationToken cancellationToken = default)
            => _context.BattleReports
            .AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => r.PlayerId == playerId)
            .OrderByDescending(r => r.FoughtAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<BattleReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.BattleReports
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(BattleReport report, CancellationToken cancellationToken = default)
        {
            await _context.BattleReports.AddAsync(report, cancellationToken);
        }
    }
}
