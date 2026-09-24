namespace EmpireIdle.Domain.Entities
{
    /// <summary>Мітка «гравець прочитав оголошення». Немає рядка — не прочитав.</summary>
    public class AnnouncementRead
    {
        public Guid AnnouncementId { get; private set; }

        public Guid PlayerId { get; private set; }

        public DateTime ReadAt { get; private set; }

        public AnnouncementRead(Guid announcementId, Guid playerId, DateTime readAt)
        {
            AnnouncementId = announcementId;
            PlayerId = playerId;
            ReadAt = readAt;
        }

        protected AnnouncementRead() { } // Для EF Core
    }
}
