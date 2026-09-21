using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class TutorialProgressRepository : ITutorialProgressRepository
    {
        private readonly AppDbContext _context;

        public TutorialProgressRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<TutorialProgress?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.TutorialProgress.FirstOrDefaultAsync(t => t.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public Task<TutorialProgress?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.TutorialProgress.AsNoTracking().FirstOrDefaultAsync(t => t.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(TutorialProgress progress, CancellationToken cancellationToken = default)
            => await _context.TutorialProgress.AddAsync(progress, cancellationToken);
    }
}
