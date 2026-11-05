using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanResearchRepository : IClanResearchRepository
    {
        private readonly AppDbContext _context;

        public ClanResearchRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ClanResearch?> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanResearch.FirstOrDefaultAsync(r => r.ClanId == clanId, cancellationToken);

        public Task<ClanResearch?> GetByClanReadOnlyAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanResearch.AsNoTracking().FirstOrDefaultAsync(r => r.ClanId == clanId, cancellationToken);

        public Task<IReadOnlyDictionary<string, int>?> GetLevelsByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanResearch
                .AsNoTracking()
                .Where(r => r.ClanId == clanId)
                .Select(r => (IReadOnlyDictionary<string, int>?)r.Levels)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<IReadOnlyDictionary<string, int>?> GetLevelsByMemberAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.ClanMembers
                .AsNoTracking()
                .Where(m => m.PlayerId == playerId)
                .Join(_context.ClanResearch, m => m.ClanId, r => r.ClanId, (_, r) => (IReadOnlyDictionary<string, int>?)r.Levels)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task AddAsync(ClanResearch research, CancellationToken cancellationToken = default)
            => await _context.ClanResearch.AddAsync(research, cancellationToken);

        public Task<int> RemoveByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanResearch.Where(r => r.ClanId == clanId).ExecuteDeleteAsync(cancellationToken);
    }
}
