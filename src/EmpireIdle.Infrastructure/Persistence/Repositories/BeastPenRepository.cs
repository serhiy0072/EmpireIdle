using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class BeastPenRepository : IBeastPenRepository
    {
        private readonly AppDbContext _context;

        public BeastPenRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public async Task<BeastPen?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            // Спершу щойно доданий у цій же одиниці роботи: інакше дві перемоги одного прогону
            // створили б два звіринці й упали на унікальному індексі
            => _context.BeastPens.Local.FirstOrDefault(p => p.PlayerId == playerId)
               ?? await Query().FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public Task<BeastPen?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => Query().AsNoTracking().FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(BeastPen pen, CancellationToken cancellationToken = default)
            => await _context.BeastPens.AddAsync(pen, cancellationToken);

        private IQueryable<BeastPen> Query()
            => _context.BeastPens
                .Include(p => p.Beasts)
                .Include(p => p.Pity)
                .AsSplitQuery();
    }
}
