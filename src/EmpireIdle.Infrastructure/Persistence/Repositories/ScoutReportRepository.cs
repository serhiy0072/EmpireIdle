using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ScoutReportRepository : IScoutReportRepository
    {
        private readonly AppDbContext _context;

        public ScoutReportRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ScoutReport report, CancellationToken cancellationToken = default)
            => await _context.ScoutReports.AddAsync(report, cancellationToken);

        public Task<List<ScoutReport>> GetRecentAsync(Guid playerId, int take, CancellationToken cancellationToken = default)
            => _context.ScoutReports
                .AsNoTracking()
                .Include(r => r.Resources)
                .Where(r => r.PlayerId == playerId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);

        public Task<List<ScoutReport>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
            => _context.ScoutReports
                .AsNoTracking()
                .Include(r => r.Resources)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<int> DeleteBeforeAsync(DateTime before, CancellationToken cancellationToken = default)
            => _context.ScoutReports.IgnoreQueryFilters()
                .Where(r => r.CreatedAt < before)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
