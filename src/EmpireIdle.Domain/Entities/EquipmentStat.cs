namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Один бонус заточки артефакта (GDD §9.12): накопичене значення всіх його рангів, у відсотках.
    /// </summary>
    public class EquipmentStat : Entity
    {
        public Guid EquipmentItemId { get; private set; }

        /// <summary>Ключ стата: Attack, CritChance, CooldownReduction…</summary>
        public string StatKey { get; private set; } = null!;

        /// <summary>
        /// Місце бонусу на предметі, 0–3: ранги йдуть по колу, тож саме позиція каже,
        /// котрий бонус качає наступний ранг. 0–1 — сталі бонуси слота, 2–3 — випадкові.
        /// </summary>
        public int Position { get; private set; }

        /// <summary>Сума ступенів усіх рангів, у відсотках.</summary>
        public double Value { get; private set; }

        public EquipmentStat(Guid id, Guid equipmentItemId, string statKey, int position, double value) : base(id)
        {
            EquipmentItemId = equipmentItemId;
            StatKey = statKey;
            Position = position;
            Value = value;
        }

        protected EquipmentStat() { } // Для EF Core

        /// <summary>Додає ступінь нового рангу.</summary>
        public void Raise(double delta) => Value += delta;
    }
}
