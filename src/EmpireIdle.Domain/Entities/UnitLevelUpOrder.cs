namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Активне замовлення на прокачку партії юнітів. На час прокачки юніти
    /// зняті з гарнізону (§5.2 GDD): недоступні для маршів, не б'ються
    /// в обороні, не рахуються в Power — точнісінько як під час тренування.
    /// </summary>
    public class UnitLevelUpOrder : Entity
    {
        public Guid GarrisonId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int FromLevel { get; private set; }
        public int ToLevel { get; private set; }
        public int Count { get; private set; }
        public DateTime CompletesAt { get; private set; }

        public UnitLevelUpOrder(Guid id, Guid garrisonId, string unitType, int fromLevel, int toLevel,
            int count, DateTime completesAt) : base(id)
        {
            GarrisonId = garrisonId;
            UnitType = unitType;
            FromLevel = fromLevel;
            ToLevel = toLevel;
            Count = count;
            CompletesAt = completesAt;
        }

        protected UnitLevelUpOrder() { } // Для EF Core

        /// <summary>Зменшує час до завершення (speedup за gems).</summary>
        public void Reduce(TimeSpan reduction) => CompletesAt -= reduction;
    }
}
