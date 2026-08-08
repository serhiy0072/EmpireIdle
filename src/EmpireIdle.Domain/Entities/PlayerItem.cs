namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Стаковий предмет в інвентарі гравця (розхідники).
    /// Один запис на тип предмета, кількість у лічильнику.
    /// </summary>
    public class PlayerItem : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Ключ типу предмета з конфіга.</summary>
        public string ItemKey { get; private set; } = null!;

        /// <summary>Кількість у стеку.</summary>
        public int Count { get; private set; }

        public PlayerItem(Guid id, Guid playerId, string itemKey, int count) : base(id)
        {
            if (count < 1)
                throw new InvalidOperationException("Item count must be at least 1.");

            PlayerId = playerId;
            ItemKey = itemKey;
            Count = count;
        }

        protected PlayerItem() { } // Для EF Core

        /// <summary>Додає до стеку.</summary>
        public void Add(int amount)
        {
            if (amount < 1)
                throw new InvalidOperationException("Amount to add must be positive.");

            Count += amount;
        }

        /// <summary>Витрачає предмети зі стеку.</summary>
        public void Consume(int amount)
        {
            if (amount < 1)
                throw new InvalidOperationException("Amount to consume must be positive.");

            if (Count < amount)
                throw new InvalidOperationException($"Not enough '{ItemKey}': need {amount}, have {Count}.");

            Count -= amount;
        }
    }
}