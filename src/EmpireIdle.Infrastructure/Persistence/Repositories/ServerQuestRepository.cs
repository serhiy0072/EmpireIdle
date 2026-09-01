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
                .Where(c => c.QuestKey == questKey && c.Amount > 0)
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.LastContributedAt)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<string>> GetCompletedWithPendingRewardsAsync(CancellationToken cancellationToken = default)
            => _context.ServerQuestProgress
                .AsNoTracking()
                .Where(p => p.State == QuestState.Completed)
                .Where(p => _context.ServerQuestContributions
                    .Any(c => c.QuestKey == p.QuestKey && c.Amount > 0 && c.RewardedAt == null))
                .Select(p => p.QuestKey)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task<(int Rank, long Amount)> GetPlayerRankAsync(string questKey, Guid playerId,
            CancellationToken cancellationToken = default)
        {
            var mine = await _context.ServerQuestContributions
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.QuestKey == questKey && c.PlayerId == playerId, cancellationToken);

            // Без внеску рангу немає — нуль, а не перше місце знизу
            if (mine is null || mine.Amount <= 0)
                return (0, 0);

            // Вище стоять ті, хто вніс більше, або стільки ж, але раніше —
            // той самий порядок, за яким роздаються нагороди
            var above = await _context.ServerQuestContributions
                .AsNoTracking()
                .CountAsync(c => c.QuestKey == questKey && c.Amount > 0
                    && (c.Amount > mine.Amount
                        || (c.Amount == mine.Amount && c.LastContributedAt < mine.LastContributedAt)),
                    cancellationToken);

            return (above + 1, mine.Amount);
        }
    }
}
