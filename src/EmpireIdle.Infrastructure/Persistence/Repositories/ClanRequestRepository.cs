using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <inheritdoc/>
    public class ClanRequestRepository : IClanRequestRepository
    {
        private readonly AppDbContext _context;

        public ClanRequestRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<ClanRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.ClanRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<ClanRequest?> GetLatestAsync(Guid clanId, Guid playerId, ClanRequestKind kind,
            CancellationToken cancellationToken = default)
            => _context.ClanRequests
                .Where(r => r.ClanId == clanId && r.PlayerId == playerId && r.Kind == kind)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanRequest>> GetPendingByClanAsync(Guid clanId, ClanRequestKind kind, DateTime utcNow,
            CancellationToken cancellationToken = default)
            => _context.ClanRequests
                .AsNoTracking()
                .Where(r => r.ClanId == clanId
                         && r.Kind == kind
                         && r.Status == ClanRequestStatus.Pending
                         && r.ExpiresAt > utcNow)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanRequest>> GetPendingByPlayerAsync(Guid playerId, ClanRequestKind kind, DateTime utcNow,
            CancellationToken cancellationToken = default)
            => _context.ClanRequests
                .AsNoTracking()
                .Where(r => r.PlayerId == playerId
                         && r.Kind == kind
                         && r.Status == ClanRequestStatus.Pending
                         && r.ExpiresAt > utcNow)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(ClanRequest request, CancellationToken cancellationToken = default)
            => await _context.ClanRequests.AddAsync(request, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanRequest>> GetPendingForPlayerAsync(Guid playerId, DateTime utcNow,
            CancellationToken cancellationToken = default)
            => _context.ClanRequests
                .Where(r => r.PlayerId == playerId
                         && r.Status == ClanRequestStatus.Pending
                         && r.ExpiresAt > utcNow)
                .ToListAsync(cancellationToken);
    }
}
