namespace EmpireIdle.Domain.Entities
{
    /// <summary>Кількість юнітів певного типу в гарнізоні села.</summary>
    public class VillageUnit : Entity
    {
        public Guid VillageId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        public VillageUnit(Guid id, Guid villageId, string unitType) : base(id)
        {
            VillageId = villageId;
            UnitType = unitType;
            Count = 0;
        }

        protected VillageUnit() { } // Для EF Core

        /// <summary>Додає навчених юнітів у гарнізон.</summary>
        public void Add(int amount) => Count += amount;
    }
}