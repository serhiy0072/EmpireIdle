namespace EmpireIdle.Domain.Entities
{
    /// <summary>Активне замовлення на тренування юнітів у казармах.</summary>
    public class UnitTrainingOrder : Entity
    {
        public Guid GarrisonId { get; private set; }
        public string UnitType { get; private set; } = null!;

        /// <summary>Рівень, на якому юніти з'являться в гарнізоні — з нуля, не 1.</summary>
        public int Level { get; private set; }
        public int Count { get; private set; }
        public DateTime CompletesAt { get; private set; }

        public UnitTrainingOrder(Guid id, Guid garrisonId, string unitType, int level, int count, DateTime completesAt) : base(id)
        {
            GarrisonId = garrisonId;
            UnitType = unitType;
            Level = level;
            Count = count;
            CompletesAt = completesAt;
        }

        protected UnitTrainingOrder() { } // Для EF Core

        /// <summary>Зменшує час до завершення.</summary>
        public void Reduce(TimeSpan reduction) => CompletesAt -= reduction;
    }
}