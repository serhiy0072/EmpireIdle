namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Асортимент офіційного магазину.</summary>
    public class ShopConfig
    {
        /// <summary>Пакети gems за реальні гроші.</summary>
        public List<GemPackConfig> GemPacks { get; set; } = new();

        /// <summary>Лутбокси за gems.</summary>
        public List<LootBoxConfig> LootBoxes { get; set; } = new();

        /// <summary>Валюта пакетів gems у форматі ISO 4217 (usd, eur…).</summary>
        public string Currency { get; set; } = "usd";
    }

    /// <summary>Пакет gems.</summary>
    public class GemPackConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>Скільки gems отримає гравець (з урахуванням бонусу).</summary>
        public int Gems { get; set; }

        /// <summary>Ціна в центах USD (Stripe працює в мінімальних одиницях).</summary>
        public int PriceCents { get; set; }

        /// <summary>Бонус понад базовий курс — лише для відображення «вигідно».</summary>
        public int BonusPercent { get; set; }
    }

    /// <summary>Лутбокс: ціна, вміст і pity-гарантія.</summary>
    public class LootBoxConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>Ціна у gems.</summary>
        public int PriceGems { get; set; }

        /// <summary>Через скільки відкриттів без легендарки вона гарантована.</summary>
        public int PityCount { get; set; }

        /// <summary>Можливий вміст із вагами.</summary>
        public List<LootDropConfig> Drops { get; set; } = new();
    }

    /// <summary>Один можливий предмет із лутбокса.</summary>
    public class LootDropConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        /// <summary>common / rare / legendary — для pity й відображення.</summary>
        public string Rarity { get; set; } = null!;

        /// <summary>Відносна вага випадіння.</summary>
        public int Weight { get; set; }
    }
}
