
namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Зберігає кількість одного типу ресурсу в селі.
    /// Використовується EF Core для маппінгу словника ресурсів.
    /// </summary>
    public class VillageResource
    {
        public Guid VillageId { get; private set; }
        public string ResourceType { get; private set; } = null!;
        public int Amount { get; private set; }

        public VillageResource(Guid villageId, string resourceType, int amount = 0)
        {
            VillageId = villageId;
            ResourceType = resourceType;
            Amount = amount;
        }

        public VillageResource() { } // Для EF Core

        /// <summary>Додає кількість ресурсу.</summary>
        public void Add(int amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to add cannot be negative.");

            Amount += amount;
        }

        /// <summary>Списує кількість ресурсу. Кидає виняток, якщо не вистачає.</summary>
        public void Subtract(int amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to subtract cannot be negative.");

            if (Amount < amount)
                throw new InvalidOperationException($"Not enough {ResourceType}: need {amount}, have {Amount}.");

            Amount -= amount;
        }
    }
}
