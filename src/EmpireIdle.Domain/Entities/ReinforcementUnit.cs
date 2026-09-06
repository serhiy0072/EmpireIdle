namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Чужі юніти, що стоять у гарнізоні як підкріплення.
    ///
    /// Власник тримається поруч із типом, бо все адресне: втрати рахуються
    /// по власниках, поранені йдуть у його госпіталь, вцілілі — у його
    /// гарнізон. Один стек на пару (власник, тип): звести двох союзників
    /// в один рядок означало б утратити, кому що повертати.
    /// </summary>
    public class ReinforcementUnit : Entity
    {
        /// <summary>Гарнізон, який приймає підкріплення.</summary>
        public Guid GarrisonId { get; private set; }

        /// <summary>Хто прислав.</summary>
        public Guid OwnerPlayerId { get; private set; }

        /// <summary>Гарнізон власника — адреса для повернення.</summary>
        public Guid OwnerGarrisonId { get; private set; }

        public string UnitType { get; private set; } = null!;

        public int Count { get; private set; }

        public DateTime ArrivedAt { get; private set; }

        public ReinforcementUnit(Guid id, Guid garrisonId, Guid ownerPlayerId, Guid ownerGarrisonId,
            string unitType, int count, DateTime utcNow) : base(id)
        {
            GarrisonId = garrisonId;
            OwnerPlayerId = ownerPlayerId;
            OwnerGarrisonId = ownerGarrisonId;
            UnitType = unitType;
            Count = count;
            ArrivedAt = utcNow;
        }

        protected ReinforcementUnit() { } // Для EF Core

        /// <summary>Долучає ще одну партію від того самого власника.</summary>
        public void Add(int amount) => Count += amount;

        /// <summary>Знімає юнітів — втрати в обороні або відкликання.</summary>
        public void Subtract(int amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to subtract cannot be negative.");

            Count = Math.Max(0, Count - amount);
        }
    }
}
