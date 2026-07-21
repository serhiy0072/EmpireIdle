namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Зона села з обмеженою кількістю слотів під будівлі певного типу.
    /// Стан конкретного села (у майбутньому — виснаження, трансформація).
    /// </summary>
    public class VillageZone : Entity
    {
        /// <summary>Ідентифікатор села.</summary>
        public Guid VillageId { get; private set; }

        /// <summary>Тип зони (plain, forest, mountain, water).</summary>
        public string Type { get; private set; } = null!;

        /// <summary>Кількість слотів під будівлі.</summary>
        public int Slots { get; private set; }

        public VillageZone(Guid id, Guid villageId, string type, int slots) : base(id)
        {
            VillageId = villageId;
            Type = type;
            Slots = slots;
        }

        protected VillageZone() { } // Для EF Core
    }
}