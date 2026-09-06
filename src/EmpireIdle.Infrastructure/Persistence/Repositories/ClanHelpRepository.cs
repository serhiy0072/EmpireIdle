using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanHelpRepository : IClanHelpRepository
    {
        private readonly AppDbContext _context;

        public ClanHelpRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<ClanHelpRequest?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default)
            => _context.ClanHelpRequests
                .Include(r => r.Helpers)
                .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanHelpRequest>> GetActiveByClanAsync(Guid clanId, DateTime utcNow,
            CancellationToken cancellationToken = default)
            => _context.ClanHelpRequests
                .AsNoTracking()
                .Include(r => r.Helpers)
                .AsSplitQuery()
                .Where(r => r.ClanId == clanId && r.ExpiresAt > utcNow)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<bool> ExistsForTargetAsync(Guid targetId, CancellationToken cancellationToken = default)
            => _context.ClanHelpRequests.AnyAsync(r => r.TargetId == targetId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(ClanHelpRequest request, CancellationToken cancellationToken = default)
            => await _context.ClanHelpRequests.AddAsync(request, cancellationToken);

        /// <inheritdoc/>
        public Task<int> RemoveExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.ClanHelpRequests
                .Where(r => r.ExpiresAt <= utcNow)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
