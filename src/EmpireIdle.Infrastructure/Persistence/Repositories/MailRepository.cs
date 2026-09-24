using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmpireIdle.Infrastructure.Persistence.Repositories
{
    public class MailRepository : IMailRepository
    {
        private readonly AppDbContext _context;

        public MailRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddLetterAsync(MailLetter letter, CancellationToken cancellationToken = default)
            => await _context.MailLetters.AddAsync(letter, cancellationToken);

        public Task<MailLetter?> GetLetterAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.MailLetters.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        public Task<List<MailLetter>> GetLettersAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.MailLetters
                .AsNoTracking()
                .Where(l => l.PlayerId == playerId && l.ExpiresAt > utcNow)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<int> CountUnreadLettersAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.MailLetters.CountAsync(l => l.PlayerId == playerId && l.ExpiresAt > utcNow && l.ReadAt == null,
                cancellationToken);

        public async Task AddAnnouncementAsync(Announcement announcement, CancellationToken cancellationToken = default)
            => await _context.Announcements.AddAsync(announcement, cancellationToken);

        public Task<Announcement?> GetAnnouncementAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Announcements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public Task<List<Announcement>> GetAnnouncementsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.Announcements
                .AsNoTracking()
                .Where(a => a.ExpiresAt > utcNow)
                .OrderByDescending(a => a.PublishedAt)
                .ToListAsync(cancellationToken);

        public async Task<HashSet<Guid>> GetReadAnnouncementIdsAsync(Guid playerId, IReadOnlyCollection<Guid> announcementIds,
            CancellationToken cancellationToken = default)
            => (await _context.AnnouncementReads
                    .AsNoTracking()
                    .Where(r => r.PlayerId == playerId && announcementIds.Contains(r.AnnouncementId))
                    .Select(r => r.AnnouncementId)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

        public Task<int> CountUnreadAnnouncementsAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default)
            => _context.Announcements.CountAsync(a => a.ExpiresAt > utcNow
                && !_context.AnnouncementReads.Any(r => r.AnnouncementId == a.Id && r.PlayerId == playerId), cancellationToken);

        public Task<bool> IsAnnouncementReadAsync(Guid announcementId, Guid playerId, CancellationToken cancellationToken = default)
            => _context.AnnouncementReads.AnyAsync(r => r.AnnouncementId == announcementId && r.PlayerId == playerId,
                cancellationToken);

        public async Task AddAnnouncementReadAsync(AnnouncementRead read, CancellationToken cancellationToken = default)
            => await _context.AnnouncementReads.AddAsync(read, cancellationToken);

        public async Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
            => await _context.MailLetters.Where(l => l.ExpiresAt <= utcNow).ExecuteDeleteAsync(cancellationToken)
                + await _context.Announcements.Where(a => a.ExpiresAt <= utcNow).ExecuteDeleteAsync(cancellationToken);
    }
}
