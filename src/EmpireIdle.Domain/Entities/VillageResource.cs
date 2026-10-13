
using EmpireIdle.Domain.Exceptions;

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
        /// <summary>Запас без стелі (GDD §4.1), тому long: int переповнився б на пізній грі.</summary>
        public long Amount { get; private set; }

        public VillageResource(Guid villageId, string resourceType, long amount = 0)
        {
            VillageId = villageId;
            ResourceType = resourceType;
            Amount = amount;
        }

        public VillageResource() { } // Для EF Core

        /// <summary>Додає кількість ресурсу.</summary>
        public void Add(long amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to add cannot be negative.");

            // Насичення замість переповнення: запас без стелі, а long.MaxValue недосяжний чесною грою
            Amount = amount > long.MaxValue - Amount ? long.MaxValue : Amount + amount;
        }

        /// <summary>Списує кількість ресурсу. Кидає виняток, якщо не вистачає.</summary>
        public void Subtract(long amount)
        {
            if (amount < 0)
                throw new InvalidOperationException("Amount to subtract cannot be negative.");

            if (Amount < amount)
                throw new NotEnoughResourcesException(ResourceType, amount, Amount);

            Amount -= amount;
        }
    }
}
