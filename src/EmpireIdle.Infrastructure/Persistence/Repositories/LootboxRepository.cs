using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    internal class LootboxRepository : ILootboxRepository
    {
        private readonly AppDbContext _context;

        public LootboxRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<LootboxPity?> GetPityAsync(Guid playerId, string lootboxKey, CancellationToken cancellationToken = default)
            => _context.LootboxPity
                .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.LootboxKey == lootboxKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<LootboxPity>> GetPitiesReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.LootboxPity
                .AsNoTracking()
                .Where(p => p.PlayerId == playerId)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddPityAsync(LootboxPity pity, CancellationToken cancellationToken = default)
        {
            await _context.LootboxPity.AddAsync(pity, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<KeyChestProgress?> GetKeyChestProgressAsync(Guid playerId, string chestKey,
            CancellationToken cancellationToken = default)
            => _context.KeyChestProgress
                .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.ChestKey == chestKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<KeyChestProgress>> GetKeyChestProgressesReadOnlyAsync(Guid playerId,
            CancellationToken cancellationToken = default)
            => _context.KeyChestProgress
                .AsNoTracking()
                .Where(p => p.PlayerId == playerId)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddKeyChestProgressAsync(KeyChestProgress progress, CancellationToken cancellationToken = default)
        {
            await _context.KeyChestProgress.AddAsync(progress, cancellationToken);
        }
    }
}
