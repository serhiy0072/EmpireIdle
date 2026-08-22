using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    /// <inheritdoc/>
    public class QuestRepository : IQuestRepository
    {
        private readonly AppDbContext _context;

        public QuestRepository(AppDbContext context) => _context = context;

        /// <inheritdoc/>
        public Task<QuestProgress?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.QuestProgress
                .Include(q => q.Objectives)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        /// <inheritdoc/>
        public Task<QuestProgress?> GetAsync(Guid playerId, string questKey, CancellationToken cancellationToken = default)
            => _context.QuestProgress
                .Include(q => q.Objectives)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.PlayerId == playerId && q.QuestKey == questKey, cancellationToken);

        /// <inheritdoc/>
        public Task<List<QuestProgress>> GetAllAsync(Guid playerId, CancellationToken cancellationToken = default)
            => _context.QuestProgress
                .Include(q => q.Objectives)
                .AsSplitQuery()
                .Where(q => q.PlayerId == playerId)
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public async Task AddAsync(QuestProgress progress, CancellationToken cancellationToken = default)
            => await _context.QuestProgress.AddAsync(progress, cancellationToken);

        public Task<List<QuestProgress>> GetByKeysAsync(Guid playerId, IReadOnlySet<string> questKeys, CancellationToken cancellationToken = default)
             => _context.QuestProgress
                .Include(q => q.Objectives)
                .AsSplitQuery()
                .Where(q => q.PlayerId == playerId && questKeys.Contains(q.QuestKey))
                .ToListAsync(cancellationToken);

        /// <inheritdoc/>
        public Task<List<QuestProgress>> GetStaleDailyAsync(IReadOnlySet<string> questKeys, DateTime startedBefore,
            int batchSize, CancellationToken cancellationToken = default)
            => _context.QuestProgress
                .Include(q => q.Objectives)
                .AsSplitQuery()
                .Where(q => questKeys.Contains(q.QuestKey) && q.StartedAt < startedBefore)
                .OrderBy(q => q.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
    }
}
