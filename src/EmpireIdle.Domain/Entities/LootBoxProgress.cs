namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Прогрес гравця за конкретним типом лутбокса: лічильник відкриттів
    /// без легендарки (pity) — гарантія, що вона випаде не пізніше N-го разу.
    /// </summary>
    public class LootBoxProgress : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Ключ типу лутбокса.</summary>
        public string BoxKey { get; private set; } = null!;

        /// <summary>Скільки відкриттів поспіль без легендарного дропу.</summary>
        public int SinceLastLegendary { get; private set; }

        /// <summary>Скільки всього відкрито (для аналітики).</summary>
        public int TotalOpened { get; private set; }

        public LootBoxProgress(Guid id, Guid playerId, string boxKey) : base(id)
        {
            PlayerId = playerId;
            BoxKey = boxKey;
            SinceLastLegendary = 0;
            TotalOpened = 0;
        }

        protected LootBoxProgress() { } // Для EF Core

        /// <summary>Фіксує відкриття: скидає лічильник при легендарці, інакше збільшує.</summary>
        public void RegisterOpening(bool wasLegendary)
        {
            TotalOpened++;
            SinceLastLegendary = wasLegendary ? 0 : SinceLastLegendary + 1;
        }
    }
}