using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Скринька: особисті листи, оголошення світу й мітки їх прочитання.</summary>
    public interface IMailRepository
    {
        Task AddLetterAsync(MailLetter letter, CancellationToken cancellationToken = default);

        Task<MailLetter?> GetLetterAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Непротерміновані листи гравця, найновіші першими.</summary>
        Task<List<MailLetter>> GetLettersAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default);

        Task<int> CountUnreadLettersAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default);

        Task AddAnnouncementAsync(Announcement announcement, CancellationToken cancellationToken = default);

        Task<Announcement?> GetAnnouncementAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Непротерміновані оголошення світу, найновіші першими.</summary>
        Task<List<Announcement>> GetAnnouncementsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Які з цих оголошень гравець уже відкривав.</summary>
        Task<HashSet<Guid>> GetReadAnnouncementIdsAsync(Guid playerId, IReadOnlyCollection<Guid> announcementIds,
            CancellationToken cancellationToken = default);

        Task<int> CountUnreadAnnouncementsAsync(Guid playerId, DateTime utcNow, CancellationToken cancellationToken = default);

        Task<bool> IsAnnouncementReadAsync(Guid announcementId, Guid playerId, CancellationToken cancellationToken = default);

        Task AddAnnouncementReadAsync(AnnouncementRead read, CancellationToken cancellationToken = default);

        /// <summary>Прибирає протерміновані листи й оголошення (разом із мітками). Повертає кількість.</summary>
        Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
    }
}
