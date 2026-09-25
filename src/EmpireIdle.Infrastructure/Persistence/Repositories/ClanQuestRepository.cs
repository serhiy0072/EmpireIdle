using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ClanQuestRepository : IClanQuestRepository
    {
        private readonly AppDbContext _context;

        public ClanQuestRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<ClanQuestProgress?> GetAsync(Guid clanId, string questKey, CancellationToken cancellationToken = default)
            => _context.ClanQuestProgress.FirstOrDefaultAsync(p => p.ClanId == clanId && p.QuestKey == questKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ClanQuestProgress>> GetByClanAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanQuestProgress.Where(p => p.ClanId == clanId).ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<string>> GetCompletedKeysAsync(Guid clanId, CancellationToken cancellationToken = default)
            => _context.ClanQuestProgress
                .Where(p => p.ClanId == clanId && p.State != QuestState.InProgress)
                .Select(p => p.QuestKey)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(ClanQuestProgress progress, CancellationToken cancellationToken = default)
            => await _context.ClanQuestProgress.AddAsync(progress, cancellationToken);
    }
}
