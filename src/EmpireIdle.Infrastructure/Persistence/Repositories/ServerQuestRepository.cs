using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ServerQuestRepository : IServerQuestRepository
    {
        private readonly AppDbContext _context;

        public ServerQuestRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<ServerQuestProgress?> GetProgressAsync(string questKey,
            CancellationToken cancellationToken = default)
            => _context.ServerQuestProgress.FirstOrDefaultAsync(p => p.QuestKey == questKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ServerQuestProgress>> GetActiveAsync(CancellationToken cancellationToken = default)
            => _context.ServerQuestProgress
                .Where(p => p.State == QuestState.InProgress)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddProgressAsync(ServerQuestProgress progress,
            CancellationToken cancellationToken = default)
            => await _context.ServerQuestProgress.AddAsync(progress, cancellationToken);

        /// <inheritdoc/>
        public Task<ServerQuestContribution?> GetContributionAsync(string questKey, Guid playerId,
            CancellationToken cancellationToken = default)
            => _context.ServerQuestContributions
                .FirstOrDefaultAsync(c => c.QuestKey == questKey && c.PlayerId == playerId, cancellationToken);

        /// <inheritdoc/>
        public async Task AddContributionAsync(ServerQuestContribution contribution,
            CancellationToken cancellationToken = default)
            => await _context.ServerQuestContributions.AddAsync(contribution, cancellationToken);

        /// <inheritdoc/>
        public async Task<long> SumContributionsAsync(string questKey,
            CancellationToken cancellationToken = default)
            => await _context.ServerQuestContributions
                .AsNoTracking()
                .Where(c => c.QuestKey == questKey)
                .SumAsync(c => c.Amount, cancellationToken);

        /// <inheritdoc/>
        public Task<List<ServerQuestContribution>> GetRankedAsync(string questKey,
            CancellationToken cancellationToken = default)
            => _context.ServerQuestContributions
                .AsNoTracking()
                .Where(c => c.QuestKey == questKey && c.Amount > 0)
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.LastContributedAt)
                .ToListAsync(cancellationToken);
    }
}
