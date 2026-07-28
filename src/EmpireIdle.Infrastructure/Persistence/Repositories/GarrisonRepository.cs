
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
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.VillageId == villageId, cancellationToken);

        /// <inheritdoc />
        public Task<List<Garrison>> GetAllAsync(CancellationToken cancellationToken)
            => _context.Garrisons
            .Include(g => g.Units)
            .Include(g => g.TrainingOrders)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        public async Task AddAsync(Garrison garrison, CancellationToken cancellationToken)
        {
            await _context.Garrisons.AddAsync(garrison, cancellationToken);
        }

    }
}
