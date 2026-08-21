using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>Кількість юнітів певного типу в гарнізоні села.</summary>
    public class VillageUnit : Entity
    {
        public Guid GarrisonId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        public VillageUnit(Guid id, Guid garrisonId, string unitType) : base(id)
        {
            GarrisonId = garrisonId;
            UnitType = unitType;
            Count = 0;
        }

        protected VillageUnit() { } // Для EF Core

        /// <summary>Додає навчених юнітів у гарнізон.</summary>
        public void Add(int amount) => Count += amount;

        /// <summary>Знімає юнітів із гарнізону.</summary>
        public void Subtract(int amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to subtract cannot be negative.");

            if (Count < amount)
                throw new NotEnoughResourcesException(UnitType, amount, Count);

            Count -= amount;
        }
    }
}
