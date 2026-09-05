
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій гарнізонів (EF Core).</summary>
    public class GarrisonRepository : IGarrisonRepository
    {
        private readonly AppDbContext _context;

        public GarrisonRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task<Garrison?> GetByVillageIdAsync(Guid villageId, CancellationToken cancellationToken)
            => _context.Garrisons
            .Include(g => g.Units)
            .Include(g => g.TrainingOrders)
            .Include(g => g.Wounded)
            .Include(g => g.Recoverable)
            .Include(g => g.Reinforcements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.VillageId == villageId, cancellationToken);

        /// <inheritdoc/>
        public Task<Garrison?> GetByVillageIdReadOnlyAsync(Guid villageId, CancellationToken cancellationToken = default)
            => _context.Garrisons
                .AsNoTracking()
                .Include(g => g.Units)
                .Include(g => g.TrainingOrders)
                .Include(g => g.Wounded)
                .Include(g => g.Recoverable)
                .Include(g => g.Reinforcements)
                .AsSplitQuery()
                .FirstOrDefaultAsync(g => g.VillageId == villageId, cancellationToken);

        public async Task AddAsync(Garrison garrison, CancellationToken cancellationToken)
        {
            await _context.Garrisons.AddAsync(garrison, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<Garrison?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Garrisons
            .Include(g => g.Units)
            .Include(g => g.TrainingOrders)
            .Include(g => g.Wounded)
            .Include(g => g.Recoverable)
            .Include(g => g.Reinforcements)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Guid>> GetIdsWithDueTrainingAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => await _context.Garrisons
            .AsNoTracking()
            .Where(g => g.TrainingOrders.Any(o => o.CompletesAt <= utcNow))
            .OrderBy(g => g.Id)
            .Take(batchSize)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<int> PurgeExpiredRecoverableAsync(DateTime utcNow, CancellationToken cancellationToken)
        {
            // Масове видалення в обхід агрегату: прострочений стек не породжує доменних подій,
            // а вантажити всі гарнізони заради нього — зайвий трафік
            return await _context.RecoverableUnits
                .Where(r => r.ExpiresAt <= utcNow)
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public Task<List<Garrison>> GetHoldingReinforcementsAsync(Guid ownerPlayerId,
            CancellationToken cancellationToken = default)
            => _context.Garrisons
                .Include(g => g.Units)
                .Include(g => g.TrainingOrders)
                .Include(g => g.Wounded)
                .Include(g => g.Recoverable)
                .Include(g => g.Reinforcements)
                .AsSplitQuery()
                .Where(g => g.Reinforcements.Any(r => r.OwnerPlayerId == ownerPlayerId))
                .ToListAsync(cancellationToken);
    }
}
