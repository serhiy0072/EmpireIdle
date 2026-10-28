using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class PlayerResearchRepository : IPlayerResearchRepository
    {
        private readonly AppDbContext _context;

        public PlayerResearchRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<PlayerResearch?> GetByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerResearch.FirstOrDefaultAsync(r => r.PlayerId == playerId, cancellationToken);

        public Task<PlayerResearch?> GetByPlayerReadOnlyAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.PlayerResearch.AsNoTracking().FirstOrDefaultAsync(r => r.PlayerId == playerId, cancellationToken);

        public Task<PlayerResearch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.PlayerResearch.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        public async Task AddAsync(PlayerResearch research, CancellationToken cancellationToken = default)
            => await _context.PlayerResearch.AddAsync(research, cancellationToken);

        public async Task<IReadOnlyList<Guid>> GetIdsWithDueResearchAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
            => await _context.PlayerResearch
                .AsNoTracking()
                .Where(r => r.CompletesAt != null && r.CompletesAt <= utcNow)
                .OrderBy(r => r.CompletesAt)
                .Select(r => r.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
