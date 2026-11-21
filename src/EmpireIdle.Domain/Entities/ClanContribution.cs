using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Внесок гравця в технології конкретного клану — для денного рейтингу. Окремий рядок, а не поле
    /// учасника: внесок переживає вихід і повернення в той самий клан (рішення 03.11).
    /// </summary>
    public class ClanContribution : Entity
    {
        public Guid ClanId { get; private set; }

        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        public long Total { get; private set; }

        /// <summary>UTC-доба, до якої належить Today.</summary>
        public DateOnly Day { get; private set; }

        public long Today { get; private set; }

        public ClanContribution(Guid id, Guid clanId, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            ClanId = clanId;
            PlayerId = playerId;
            ServerId = serverId;
            Day = DateOnly.FromDateTime(utcNow);
        }

        protected ClanContribution() { } // для EF Core

        public void Add(int points, DateTime utcNow)
        {
            var today = DateOnly.FromDateTime(utcNow);

            if (Day != today)
            {
                Day = today;
                Today = 0;
            }

            Total += points;
            Today += points;

            RaiseDomainEvent(new ClanOfferingMade(PlayerId, ClanId, points, utcNow));
        }

        /// <summary>Внесок за цю добу: вчорашній лічильник уже не рахується, хоч рядок ще не оновлено.</summary>
        public long TodayAt(DateTime utcNow) => Day == DateOnly.FromDateTime(utcNow) ? Today : 0;
    }
}
