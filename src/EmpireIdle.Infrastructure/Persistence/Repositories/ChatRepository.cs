using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly AppDbContext _context;

        public ChatRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
            => await _context.ChatMessages.AddAsync(message, cancellationToken);

        public Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        public Task<int> CountSentSinceAsync(Guid senderId, DateTime since, CancellationToken cancellationToken = default)
            => _context.ChatMessages.CountAsync(m => m.SenderId == senderId && m.SentAt > since, cancellationToken);

        public async Task<DateTime?> GetOldestSentSinceAsync(Guid senderId, DateTime since,
            CancellationToken cancellationToken = default)
            => await _context.ChatMessages
                .Where(m => m.SenderId == senderId && m.SentAt > since)
                .OrderBy(m => m.SentAt)
                .Select(m => (DateTime?)m.SentAt)
                .FirstOrDefaultAsync(cancellationToken);

        public Task<List<ChatMessage>> GetServerHistoryAsync(DateTime? before, int take, CancellationToken cancellationToken = default)
            => Page(_context.ChatMessages.Where(m => m.Channel == ChatChannel.Server), before, take, cancellationToken);

        public Task<List<ChatMessage>> GetClanHistoryAsync(Guid clanId, DateTime? before, int take,
            CancellationToken cancellationToken = default)
            => Page(_context.ChatMessages.Where(m => m.Channel == ChatChannel.Clan && m.ClanId == clanId), before, take,
                cancellationToken);

        public Task<List<ChatMessage>> GetPrivateHistoryAsync(Guid playerId, Guid partnerId, DateTime? before, int take,
            CancellationToken cancellationToken = default)
            => Page(_context.ChatMessages.Where(m => m.Channel == ChatChannel.Private
                    && ((m.SenderId == playerId && m.RecipientId == partnerId)
                        || (m.SenderId == partnerId && m.RecipientId == playerId))),
                before, take, cancellationToken);

        public async Task<List<ChatMessage>> GetLatestPrivateMessagesAsync(Guid playerId, int take,
            CancellationToken cancellationToken = default)
        {
            // Розмова ідентифікується співрозмовником; остання — найсвіжіша в групі
            var mine = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.Channel == ChatChannel.Private && (m.SenderId == playerId || m.RecipientId == playerId))
                .OrderByDescending(m => m.SentAt)
                .Take(take * 20)
                .ToListAsync(cancellationToken);

            return mine
                .GroupBy(m => m.SenderId == playerId ? m.RecipientId : m.SenderId)
                .Select(g => g.First())
                .Take(take)
                .ToList();
        }

        public async Task<Dictionary<Guid, string>> GetTranslationsAsync(IReadOnlyCollection<Guid> messageIds, string language,
            CancellationToken cancellationToken = default)
            => await _context.ChatTranslations
                .AsNoTracking()
                .Where(t => messageIds.Contains(t.MessageId) && t.Language == language)
                .ToDictionaryAsync(t => t.MessageId, t => t.Text, cancellationToken);

        public async Task AddTranslationAsync(ChatTranslation translation, CancellationToken cancellationToken = default)
            => await _context.ChatTranslations.AddAsync(translation, cancellationToken);

        public Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken cancellationToken = default)
            => _context.ChatMessages.Where(m => m.SentAt < before).ExecuteDeleteAsync(cancellationToken);

        /// <summary>Останні take до моменту before, але в хронологічному порядку — так їх малює чат.</summary>
        private static async Task<List<ChatMessage>> Page(IQueryable<ChatMessage> query, DateTime? before, int take,
            CancellationToken cancellationToken)
        {
            if (before is { } moment)
                query = query.Where(m => m.SentAt < moment);

            var page = await query
                .AsNoTracking()
                .OrderByDescending(m => m.SentAt)
                .Take(take)
                .ToListAsync(cancellationToken);

            page.Reverse();
            return page;
        }
    }
}
