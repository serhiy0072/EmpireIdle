namespace EmpireIdle.Domain.Entities
{
    /// <summary>Поранені юніти в Госпіталі, що очікують лікування.</summary>
    public class WoundedUnit : Entity
    {
        public Guid GarrisonId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        public WoundedUnit(Guid id, Guid garrisonId, string unitType, int count) : base(id)
        {
            GarrisonId = garrisonId;
            UnitType = unitType;
            Count = count;
        }

        protected WoundedUnit() { } // Для EF Core

        /// <summary>Додає поранених.</summary>
        public void Add(int amount) => Count += amount;

        /// <summary>Забирає вилікуваних.</summary>
        public void Reduce(int amount) => Count = Math.Max(0, Count - amount);
    }
}
