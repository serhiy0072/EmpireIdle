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
}
