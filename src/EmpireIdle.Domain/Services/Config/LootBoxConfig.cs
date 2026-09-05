namespace EmpireIdle.Domain.Services.Config
{
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
}
