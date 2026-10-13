namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Журнал роллів заточки артефакта: ранг і сід.
    ///
    /// Зберігається сід, а не результат: ролл відтворюється з нього повністю,
    /// і рядок лишається коротким. Той самий підхід, що й у BattleReport.
    /// </summary>
    public class EquipmentRoll : Entity
    {
        public Guid EquipmentItemId { get; private set; }

        /// <summary>Заточка, на яку підняли: ранг +1…+20.</summary>
        public int Mastery { get; private set; }

        public int Seed { get; private set; }

        public DateTime RolledAt { get; private set; }

        public EquipmentRoll(Guid id, Guid equipmentItemId, int mastery, int seed, DateTime utcNow) : base(id)
        {
            EquipmentItemId = equipmentItemId;
            Mastery = mastery;
            Seed = seed;
            RolledAt = utcNow;
        }

        protected EquipmentRoll() { } // Для EF Core
    }
}
