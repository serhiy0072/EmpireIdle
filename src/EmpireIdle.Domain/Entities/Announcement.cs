using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Оголошення світу (GDD §7.4): один рядок на світ, а не копія на кожного
    /// гравця — новина на п'ять тисяч гравців це один текст. Хто прочитав,
    /// видно з AnnouncementRead, що створюється лише при відкритті.
    /// </summary>
    public class Announcement : Entity
    {
        public int ServerId { get; private set; }

        public AnnouncementKind Kind { get; private set; }

        public string Title { get; private set; } = null!;

        public string Body { get; private set; } = null!;

        public DateTime PublishedAt { get; private set; }

        public DateTime ExpiresAt { get; private set; }

        public Announcement(Guid id, int serverId, AnnouncementKind kind, string title, string body, DateTime utcNow,
            DateTime expiresAt) : base(id)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("An announcement needs a title.", nameof(title));

            if (expiresAt <= utcNow)
                throw new ArgumentOutOfRangeException(nameof(expiresAt), expiresAt, "An announcement must expire in the future.");

            ServerId = serverId;
            Kind = kind;
            Title = title;
            Body = body;
            PublishedAt = utcNow;
            ExpiresAt = expiresAt;
        }

        protected Announcement() { } // Для EF Core
    }
}
