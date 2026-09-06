namespace EmpireIdle.Domain.Entities
{
    /// <summary>Одна характеристика екземпляра спорядження.</summary>
    public class EquipmentStat : Entity
    {
        public Guid EquipmentItemId { get; private set; }

        /// <summary>Ключ стата: Attack, Defense, Speed…</summary>
        public string StatKey { get; private set; } = null!;

        public double Value { get; private set; }

        public EquipmentStat(Guid id, Guid equipmentItemId, string statKey, double value) : base(id)
        {
            EquipmentItemId = equipmentItemId;
            StatKey = statKey;
            Value = value;
        }

        protected EquipmentStat() { } // Для EF Core
    }
}
