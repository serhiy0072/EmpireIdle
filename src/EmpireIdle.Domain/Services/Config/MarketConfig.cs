namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Правила ринку гравців (GDD §8.8). Усі числа — заглушки до Режисера.
    ///
    /// Ціна тримається в коридорі навколо медіани «золота за одиницю»:
    /// для спорядження й героїв одиниця — Power, для стакових предметів —
    /// штука. Так предмети різної сили порівнюються однією шкалою.
    /// </summary>
    public class MarketConfig
    {
        /// <summary>
        /// Будівля ринку: відкриває його й задає ліміт лотів своїм рівнем.
        /// null — ринку в цій грі немає (решкін без торгівлі, тестові конфіги).
        /// </summary>
        public string? BuildingKey { get; set; }

        /// <summary>Податок на виставлення, частка ціни. Спалюється й не повертається.</summary>
        public double ListingTaxShare { get; set; } = 0.05;

        /// <summary>Скільки годин лот висить на ринку.</summary>
        public int ListingHours { get; set; } = 48;

        /// <summary>Скільки годин куплений екземпляр не можна виставити знову.</summary>
        public int ResaleCooldownHours { get; set; } = 72;

        /// <summary>Активних лотів без урахування рівня ринку.</summary>
        public int BaseListings { get; set; } = 1;

        /// <summary>Додаткових лотів за кожен рівень будівлі ринку.</summary>
        public int ListingsPerBuildingLevel { get; set; } = 1;

        /// <summary>Коридор ціни навколо медіани: 0.3 — ±30%.</summary>
        public double CorridorShare { get; set; } = 0.3;

        /// <summary>За скільки годин продажі входять у медіану.</summary>
        public int MedianWindowHours { get; set; } = 48;

        /// <summary>Скільки продажів треба, щоб медіані довіряти; менше — діє якір.</summary>
        public int MinSalesForMedian { get; set; } = 5;

        /// <summary>Частка найдешевших і найдорожчих продажів, що відкидаються як викиди.</summary>
        public double OutlierTrimShare { get; set; } = 0.1;

        /// <summary>
        /// Межі медіани відносно якоря: медіана не відходить від нього
        /// далі, ніж у [Min; Max] разів. Інакше коридор проганяється
        /// вошем між двома акаунтами.
        /// </summary>
        public double MedianMinAnchorShare { get; set; } = 0.5;

        public double MedianMaxAnchorShare { get; set; } = 2.0;

        /// <summary>Якір для предметів із ціною в gems: золота за один gem.</summary>
        public double GoldPerGem { get; set; } = 100;

        /// <summary>
        /// Якір для спорядження й героїв: золота за одиницю Power за категорією
        /// (ключ — категорія ціни, як у MarketPricing.CategoryOf).
        /// </summary>
        public Dictionary<string, double> GoldPerPower { get; set; } = new();
    }
}
