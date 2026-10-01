namespace EmpireIdle.Domain.Entities
{
    /// <summary>Членство гравця в клані.</summary>
    public class ClanMember : Entity
    {
        public Guid ClanId { get; private set; }

        public Guid PlayerId { get; private set; }

        public Guid RoleId { get; private set; }

        public DateTime JoinedAt { get; private set; }

        /// <summary>
        /// Остання дія в клані. За нею визначається неактивність лідера
        /// для автопередачі: мертвий лідер інакше вбиває клан.
        /// </summary>
        public DateTime LastActiveAt { get; private set; }

        /// <summary>Скільки очок вкладу цей учасник приніс клану — для рейтингу всередині клану.</summary>
        public long Contribution { get; private set; }

        public ClanMember(Guid id, Guid clanId, Guid playerId, Guid roleId, DateTime utcNow) : base(id)
        {
            ClanId = clanId;
            PlayerId = playerId;
            RoleId = roleId;
            JoinedAt = utcNow;
            LastActiveAt = utcNow;
        }

        protected ClanMember() { } // для EF Core

        internal void AssignRole(Guid roleId) => RoleId = roleId;

        internal void Touch(DateTime utcNow) => LastActiveAt = utcNow;

        internal void AddContribution(long amount) => Contribution += amount;
    }
}
