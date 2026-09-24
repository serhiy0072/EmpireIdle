namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Запис про падіння міста: хто, кого, звідки й куди виселив.
    ///
    /// Історія, а не стан: з неї рахується ліміт виселень нападника за вікно
    /// (GDD §2.6), на неї посилається лист у скриньці, і вона ж — доказ при
    /// скаргах. Назва нападника — знімок на момент падіння, як у звітах боїв.
    /// </summary>
    public class VillageFall : Entity
    {
        public int ServerId { get; private set; }

        /// <summary>Власник виселеного села.</summary>
        public Guid PlayerId { get; private set; }

        public Guid AttackerPlayerId { get; private set; }

        public string AttackerVillageName { get; private set; } = null!;

        public int FromX { get; private set; }

        public int FromY { get; private set; }

        public int ToX { get; private set; }

        public int ToY { get; private set; }

        public DateTime ShieldUntil { get; private set; }

        public DateTime OccurredAt { get; private set; }

        public VillageFall(Guid id, int serverId, Guid playerId, Guid attackerPlayerId, string attackerVillageName,
            int fromX, int fromY, int toX, int toY, DateTime shieldUntil, DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            PlayerId = playerId;
            AttackerPlayerId = attackerPlayerId;
            AttackerVillageName = attackerVillageName;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            ShieldUntil = shieldUntil;
            OccurredAt = utcNow;
        }

        protected VillageFall() { } // Для EF Core
    }
}
