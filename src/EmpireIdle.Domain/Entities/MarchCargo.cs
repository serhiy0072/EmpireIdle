namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Ресурси, які армія везе додому. Живуть на марші, а не в селі:
    /// здобич стає доступною лише після прибуття, і саме тому набіг
    /// на далеке село ризикованіший за близький.
    /// </summary>
    public class MarchCargo : Entity
    {
        public Guid MarchId { get; private set; }

        public string ResourceType { get; private set; } = null!;

        public int Amount { get; private set; }

        public MarchCargo(Guid id, Guid marchId, string resourceType, int amount) : base(id)
        {
            MarchId = marchId;
            ResourceType = resourceType;
            Amount = amount;
        }

        protected MarchCargo() { } // Для EF Core

        public void Add(int amount) => Amount += amount;
    }
}
