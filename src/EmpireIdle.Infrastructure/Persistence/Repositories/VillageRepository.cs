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
        public async Task<IReadOnlyList<Guid>> GetIdsWithDueConstructionsAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => await _context.Villages
                .AsNoTracking()
                .Where(v => v.Buildings.Any(b => b.ConstructionCompletesAt != null && b.ConstructionCompletesAt <= utcNow))
                .OrderBy(v => v.Id)
                .Take(batchSize)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<int> GetMedianMainBuildingLevelAsync(string mainBuildingKey,
            CancellationToken cancellationToken = default)
        {
            var levels = _context.Buildings
                .AsNoTracking()
                .Where(b => b.Type == mainBuildingKey)
                .Select(b => b.Level.Value);

            var count = await levels.CountAsync(cancellationToken);

            if (count == 0)
                return 0;

            // OrderBy + Skip, а не PERCENTILE_CONT: тягне один рядок
            // і не прив'язує репозиторій до діалекту Postgres
            return await levels
                .OrderBy(level => level)
                .Skip(count / 2)
                .Take(1)
                .FirstAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => _context.Villages.AsNoTracking().CountAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<Village>> GetAllWithBuildingsAsync(CancellationToken cancellationToken = default)
            => _context.Villages
                .AsNoTracking()
                .Include(v => v.Buildings)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
    }
}
