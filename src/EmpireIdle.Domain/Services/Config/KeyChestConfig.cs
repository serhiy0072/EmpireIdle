namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Ключова скриня (рішення 12.10.2026): таблиця з Lootboxes, яку не купують, а відкривають —
    /// кілька разів на добу безкоштовно, понад це ключем із крамниці.
    /// </summary>
    public class KeyChestConfig
    {
        /// <summary>Ключ таблиці в Lootboxes; він же — ключ скрині для клієнта.</summary>
        public string LootboxKey { get; set; } = null!;

        /// <summary>Предмет-ключ (Type "chestkey"): одне відкриття — один ключ.</summary>
        public string KeyItemKey { get; set; } = null!;

        /// <summary>Скільки безкоштовних відкриттів за добу UTC; 0 — лише ключем.</summary>
        public int FreePerDay { get; set; }

        /// <summary>Перезарядка між безкоштовними відкриттями, у хвилинах: рідкісна — 5, унікальна — 2 880.</summary>
        public int FreeCooldownMinutes { get; set; }
    }
}
