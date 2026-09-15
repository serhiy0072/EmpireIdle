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

        /// <summary>
        /// Підсилює стат. Артефакти качають випадкові стати на 12, 16 і 20
        /// рівнях — ролер обирає які, рядок лише зберігає результат.
        /// </summary>
        public void Raise(double delta) => Value += delta;
    }
}
