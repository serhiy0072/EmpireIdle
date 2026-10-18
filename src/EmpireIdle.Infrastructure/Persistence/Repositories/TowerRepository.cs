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
    }
}
