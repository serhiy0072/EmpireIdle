using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію Player через EF Core.
    /// </summary>
    public class PlayerRepository : IPlayerRepository
    {
        private readonly AppDbContext _context;
        public PlayerRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task AddAsync(Player entity, CancellationToken cancellationToken = default)
            => await _context.Players.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public Task<Player?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Players.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<Player?> GetByUserIdAsync(string userId, int serverId, CancellationToken cancellationToken = default)
            => _context.Players
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ServerId == serverId, cancellationToken);

        /// <inheritdoc/>
        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> GetIdsAfterAsync(Guid? after, int take, CancellationToken cancellationToken = default)
            => await _context.Players
                .AsNoTracking()
                .Where(p => after == null || p.Id.CompareTo(after.Value) > 0)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<Guid>> GetIdsByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => await _context.Players
                .AsNoTracking()
                .Where(p => p.ClanId == clanId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

        public async Task<Dictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
            => await _context.Players
                .AsNoTracking()
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Username, cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> TouchLastSeenAsync(Guid playerId, DateTime utcNow, TimeSpan threshold, CancellationToken cancellationToken = default)
        {
            var staleBefore = utcNow - threshold;

            var updated = await _context.Players
                .Where(p => p.Id == playerId && p.LastSeenAt < staleBefore)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastSeenAt, utcNow), cancellationToken);

            return updated > 0;
        }

        /// <inheritdoc/>
        public Task<Dictionary<Guid, DateTime>> GetLastSeenAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default)
            => _context.Players
                .AsNoTracking()
                .Where(p => playerIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.LastSeenAt, cancellationToken);
    }
}
