namespace EmpireIdle.Domain.Entities
{
    /// <summary>Активне замовлення на тренування юнітів у казармах.</summary>
    public class UnitTrainingOrder : Entity
    {
        public Guid VillageId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }
        public DateTime CompletesAt { get; private set; }

        public UnitTrainingOrder(Guid id, Guid villageId, string unitType, int count, DateTime completesAt) : base(id)
        {
            VillageId = villageId;
            UnitType = unitType;
            Count = count;
            CompletesAt = completesAt;
        }

        protected UnitTrainingOrder() { } // Для EF Core
    }
}