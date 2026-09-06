namespace EmpireIdle.Domain.Entities
{
    /// <summary>Одна допомога від одного гравця.</summary>
    public class ClanHelpContribution : Entity
    {
        public Guid RequestId { get; private set; }

        public Guid HelperId { get; private set; }

        public DateTime HelpedAt { get; private set; }

        public ClanHelpContribution(Guid id, Guid requestId, Guid helperId, DateTime utcNow) : base(id)
        {
            RequestId = requestId;
            HelperId = helperId;
            HelpedAt = utcNow;
        }

        protected ClanHelpContribution() { } // для EF Core
    }
}
