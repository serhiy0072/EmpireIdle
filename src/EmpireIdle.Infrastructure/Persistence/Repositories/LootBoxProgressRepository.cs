using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    internal class LootBoxProgressRepository : ILootBoxProgressRepository
    {
        private readonly AppDbContext _context;

        public LootBoxProgressRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<LootBoxProgress?> GetAsync(Guid playerId, string boxKey, CancellationToken cancellationToken = default)
            => _context.LootBoxProgress
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && p.BoxKey == boxKey, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(LootBoxProgress progress, CancellationToken cancellationToken = default)
        {
            await _context.LootBoxProgress.AddAsync(progress, cancellationToken);
        }
    }
}
