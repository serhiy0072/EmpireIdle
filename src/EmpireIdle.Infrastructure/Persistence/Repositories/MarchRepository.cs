using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <summary>Репозиторій походів (EF Core).</summary>
    public class MarchRepository : IMarchRepository
    {
        private readonly AppDbContext _context;

        public MarchRepository(AppDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public Task<List<March>> GetActiveByGarrisonAsync(Guid garrisonId, CancellationToken cancellationToken = default)
            => _context.Marches
            .Include(m => m.Units)
            .Where(m => m.GarrisonId == garrisonId && m.State != MarchState.Completed)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<March>> GetDueAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => _context.Marches
            .AsNoTracking()
            .Where(m => m.State != MarchState.Completed && m.ArrivesAt <= utcNow)
            .OrderBy(m => m.ArrivesAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<March?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Marches
            .Include(m => m.Units)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(March march, CancellationToken cancellationToken = default)
            =>  await _context.Marches.AddAsync(march, cancellationToken);
        
    }
}

