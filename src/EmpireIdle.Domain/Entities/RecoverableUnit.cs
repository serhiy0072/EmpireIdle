namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Юніти, яких можна повернути в гарнізон за gems до спливання дедлайну.
    /// Один стек = один бій × один тип юніта (у кожного свій ExpiresAt).
    /// </summary>
    public class RecoverableUnit : Entity
    {
        public Guid GarrisonId { get; private set; }
        public Guid BattleReportId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        /// <summary>Після цього моменту стек згорає.</summary>
        public DateTime ExpiresAt { get; private set; }

        public RecoverableUnit(Guid id, Guid garrisonId, Guid battleReportId,
            string unitType, int count, DateTime expiresAt) : base(id)
        {
            GarrisonId = garrisonId;
            BattleReportId = battleReportId;
            UnitType = unitType;
            Count = count;
            ExpiresAt = expiresAt;
        }

        protected RecoverableUnit() { } // Для EF Core

        /// <summary>Чи стек ще дійсний.</summary>
        public bool IsActive(DateTime utcNow) => ExpiresAt > utcNow;

        /// <summary>Забирає викуплених.</summary>
        public void Reduce(int amount) => Count = Math.Max(0, Count - amount);
    }
}