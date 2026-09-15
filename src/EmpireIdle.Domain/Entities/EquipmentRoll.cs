namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Журнал роллів артефакта: рівень і сід.
    ///
    /// Зберігається сід, а не результат: ролл відтворюється з нього повністю,
    /// і рядок лишається коротким. Той самий підхід, що й у BattleReport.
    /// </summary>
    public class EquipmentRoll : Entity
    {
        public Guid EquipmentItemId { get; private set; }

        /// <summary>Рівень, на який качали. Нуль — стартовий набір.</summary>
        public int Level { get; private set; }

        public int Seed { get; private set; }

        public DateTime RolledAt { get; private set; }

        public EquipmentRoll(Guid id, Guid equipmentItemId, int level, int seed, DateTime utcNow) : base(id)
        {
            EquipmentItemId = equipmentItemId;
            Level = level;
            Seed = seed;
            RolledAt = utcNow;
        }

        protected EquipmentRoll() { } // Для EF Core
    }
}
