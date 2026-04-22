using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Будівля в селі гравця. Виробляє ресурси з часом.
    /// </summary>
    public class Building : Entity
    {
        /// <summary>Тип будівлі (з game-config).</summary>
        public string Type { get; private set; } = null!;

        /// <summary>Поточний рівень будівлі.</summary>
        public BuildingLevel Level { get; private set; } = null!;

        /// <summary>Ідентифікатор села якому належить будівля.</summary>
        public Guid VillageId { get; private set; }

        /// <summary>Час останнього збору ресурсів.</summary>
        public DateTime LastCollectedAt { get; private set; }

        public Building(Guid id, Guid villageId, string type) : base(id)
        {
            VillageId = villageId;
            Type = type;
            Level = BuildingLevel.Initial;
            LastCollectedAt = DateTime.UtcNow;
        }

        protected Building() { }// Для EF Core

        /// <summary>
        /// Розраховує кількість ресурсів вироблених за вказаний час.
        /// Базова формула: рівень * 10 одиниць за хвилину.
        /// </summary>
        /// <param name="elapsed">Час що минув.</param>
        public Dictionary<string, ResourceAmount> CalculateProduction(TimeSpan elapsed)
        {
            // Рахуємо через double щоб зберегти дробові хвилини.
            // Без цього тік кожні 30с даватиме 0 ресурсів (int)0.5 = 0.
            var amount = new ResourceAmount((int)(Level.Value * 10 * elapsed.TotalMinutes));

            // Тип ресурсу визначатиметься через GameConfig у наступних фазах
            return new Dictionary<string, ResourceAmount>
            {
                [ResourceType.Gold] = amount
            };
        }
    }
}
