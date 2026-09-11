using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій героїв (EF Core).</summary>
    public class HeroRepository : IHeroRepository
    {
        private readonly AppDbContext _context;

        public HeroRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<Hero>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Heroes
            .Where(h => h.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<Hero?> GetByKeyAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default)
            => _context.Heroes
            .FirstOrDefaultAsync(h => h.PlayerId == playerId && h.HeroKey == heroKey, cancellationToken);

        /// <inheritdoc/>
        public Task<Hero?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Heroes
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<int> CountAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Heroes
            .CountAsync(h => h.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(Hero hero, CancellationToken cancellationToken = default)
        {
            await _context.Heroes.AddAsync(hero, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<HeroShardProgress?> GetShardsAsync(Guid playerId, string heroKey, CancellationToken cancellationToken = default)
            => _context.HeroShards
            .FirstOrDefaultAsync(s => s.PlayerId == playerId && s.HeroKey == heroKey, cancellationToken);

        /// <inheritdoc/>
        public async Task AddShardsAsync(HeroShardProgress progress, CancellationToken cancellationToken = default)
        {
            await _context.HeroShards.AddAsync(progress, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<HeroLevelOrder?> GetActiveOrderAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.HeroLevelOrders
            .FirstOrDefaultAsync(o => o.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddOrderAsync(HeroLevelOrder order, CancellationToken cancellationToken = default)
        {
            await _context.HeroLevelOrders.AddAsync(order, cancellationToken);
        }

        /// <inheritdoc/>
        public void RemoveOrder(HeroLevelOrder order) => _context.HeroLevelOrders.Remove(order);
    }
}
