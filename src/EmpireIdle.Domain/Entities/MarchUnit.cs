namespace EmpireIdle.Domain.Entities
{
    /// <summary>Загін у складі походу.</summary>
    public class MarchUnit : Entity
    {
        public Guid MarchId { get; private set; }
        public string UnitType { get; private set; } = null!;
        public int Count { get; private set; }

        public MarchUnit(Guid id, Guid marchId, string unitType, int count) : base(id)
        {
            MarchId = marchId;
            UnitType = unitType;
            Count = count;
        }

        protected MarchUnit() { } // Для EF Core

        /// <summary>Зменшує кількість юнітів у загоні (втрати в бою).</summary>
        public void Reduce(int amount)
        {
            Count = Math.Max(0, Count - amount);
        }


    }
}
