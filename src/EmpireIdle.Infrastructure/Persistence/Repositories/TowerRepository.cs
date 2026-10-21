using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class TowerRepository : ITowerRepository
    {
        private readonly AppDbContext _context;

        public TowerRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<TowerProgress?> GetProgressAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.TowerProgress.FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        public Task<TowerProgress?> GetProgressReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.TowerProgress.AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        public async Task AddProgressAsync(TowerProgress progress, CancellationToken cancellationToken = default)
            => await _context.TowerProgress.AddAsync(progress, cancellationToken);

        public Task<List<TowerProgress>> GetTopAsync(int count, CancellationToken cancellationToken = default)
            => _context.TowerProgress
                .AsNoTracking()
                .Where(p => p.HighestFloor > 0)
                .OrderByDescending(p => p.HighestFloor)
                .ThenBy(p => p.ReachedAt)
                .Take(count)
                .ToListAsync(cancellationToken);

        public async Task<int> GetRankAsync(TowerProgress progress, CancellationToken cancellationToken = default)
        {
            // Вище стоїть той, хто піднявся вище, або на той самий поверх, але раніше
            var above = await _context.TowerProgress
                .AsNoTracking()
                .CountAsync(p => p.HighestFloor > progress.HighestFloor
                    || (p.HighestFloor == progress.HighestFloor && p.ReachedAt < progress.ReachedAt), cancellationToken);

            return above + 1;
        }
    }
}
