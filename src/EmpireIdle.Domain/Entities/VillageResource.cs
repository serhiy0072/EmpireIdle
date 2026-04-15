
namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Зберігає кількість одного типу ресурсу в селі.
    /// Використовується EF Core для маппінгу словника ресурсів.
    /// </summary>
    public class VillageResource
    {
        public Guid VillageId { get; set; }
        public string ResourceType { get; set; } = null!;
        public int Amount { get; set; }
    }
}
