using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій активних ефектів (EF Core).</summary>
    public class ActiveEffectRepository : IActiveEffectRepository
    {
        private readonly AppDbContext _context;

        public ActiveEffectRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<ActiveEffect>> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.ActiveEffects
            .Where(e => e.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<ActiveEffect?> GetAsync(Guid playerId, EffectTarget target, CancellationToken cancellationToken = default)
            => _context.ActiveEffects
            .FirstOrDefaultAsync(e => e.PlayerId == playerId && e.Target == target, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(ActiveEffect effect, CancellationToken cancellationToken = default)
        {
            await _context.ActiveEffects.AddAsync(effect, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> RemoveExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.ActiveEffects
            .Where(e => e.ExpiresAt <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }
}