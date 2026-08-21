using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Реалізація репозиторію Village через EF Core.
    /// </summary>
    public class VillageRepository : IVillageRepository
    {
        private readonly AppDbContext _context;

        public VillageRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task AddAsync(Village entity, CancellationToken cancellationToken = default) 
            => await _context.Villages.AddAsync(entity, cancellationToken);

        /// <inheritdoc/>
        public Task<Village?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Villages
            .Include(v => v.Buildings)
            .Include(v => v.Resources)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<Village?> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Villages
            .Include(v => v.Buildings)
            .Include(v => v.Resources)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public Task<Village?> GetByPlayerIdReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.Villages
            .Include(v => v.Buildings)
            .Include(v => v.Resources)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public Task<List<Village>> GetWithDueConstructionsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.Villages
            .Include(v => v.Buildings)
            .Include(v => v.Resources)
            .AsSplitQuery()
            // Тягнемо лише ті села, де є що завершувати
            .Where(v => v.Buildings.Any(b => b.ConstructionCompletesAt != null && b.ConstructionCompletesAt <= utcNow))
            .ToListAsync(cancellationToken);

    }
}
