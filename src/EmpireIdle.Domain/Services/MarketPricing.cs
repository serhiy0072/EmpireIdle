using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Дозволений діапазон ціни за одиницю. Межі цілі: мінімум округлюється
    /// вгору, максимум — униз, тож обидві межі самі проходять перевірку.
    /// </summary>
    public readonly record struct PriceCorridor(double MinPerUnit, double MaxPerUnit)
    {
        public int MinPrice(double units) => Math.Max(1, (int)Math.Ceiling(MinPerUnit * units));

        public int MaxPrice(double units) => Math.Max(MinPrice(units), (int)Math.Floor(MaxPerUnit * units));

        public bool Allows(int price, double units) => price >= MinPrice(units) && price <= MaxPrice(units);
    }

    /// <summary>
    /// Ціни ринку (GDD §8.8): категорія товару, якір, медіана продажів
    /// і коридор навколо неї. Категорії пишуться через крапку («hero.Rare»,
    /// «item.teleport»): двокрапка в ключі конфіга .NET означає вкладеність.
    ///
    /// Одиниця ціни — Power для спорядження й героїв і штука для стакових
    /// предметів. Інакше меч +15 і меч +0 мусили б продаватись у тому
    /// самому коридорі, а коридор втратив би сенс.
    ///
    /// Чиста функція від конфіга й переданих чисел: продажі читає джоб.
    /// </summary>
    public class MarketPricing
    {
        private readonly MarketConfig _market;
        private readonly IReadOnlyDictionary<string, int> _gemPrices;

        public MarketPricing(GameConfig config)
        {
            _market = config.Market;
            _gemPrices = config.Shop.Items
                .GroupBy(offer => offer.ItemKey)
                .ToDictionary(g => g.Key, g => g.Min(offer => offer.PriceGems));
        }

        /// <summary>Категорія спорядження: зброя й артефакти мають різну ціну сили.</summary>
        public static string CategoryOf(EquipmentSlot slot)
            => slot == EquipmentSlot.Weapon ? "weapon" : "artifact";

        /// <summary>Категорія героя: унікальний герой тієї самої сили цінніший за звичайного.</summary>
        public static string CategoryOf(Rarity heroRank) => $"hero.{heroRank}";

        /// <summary>Стаковий предмет — сам собі категорія.</summary>
        public static string CategoryOfItem(string itemKey) => $"item.{itemKey}";

        /// <summary>
        /// Абсолютний якір ціни за одиницю. Для стакових — ціна в крамниці
        /// в gems за курсом; для сили — ціна одиниці Power з конфіга.
        /// null — якоря немає, і товар торгуватись не може: коридор
        /// без якоря проганяється вошем з першого ж продажу.
        /// </summary>
        public double? AnchorPerUnit(string pricingKey)
        {
            const string itemPrefix = "item.";

            if (pricingKey.StartsWith(itemPrefix, StringComparison.Ordinal))
                return _gemPrices.TryGetValue(pricingKey[itemPrefix.Length..], out var gems)
                    ? gems * _market.GoldPerGem
                    : null;

            return _market.GoldPerPower.TryGetValue(pricingKey, out var perPower) ? perPower : null;
        }

        /// <summary>
        /// Медіана ціни за одиницю з обрізаними викидами. null — продажів
        /// замало, щоб їй довіряти, і діє якір.
        /// </summary>
        public double? Median(IEnumerable<double> pricesPerUnit)
        {
            var sorted = pricesPerUnit.Order().ToList();

            if (sorted.Count < _market.MinSalesForMedian)
                return null;

            var trim = (int)Math.Floor(sorted.Count * _market.OutlierTrimShare);
            var kept = sorted.Skip(trim).Take(sorted.Count - 2 * trim).ToList();

            // Обрізання не лишає порожнього списку, поки частка менша за половину —
            // межу тримає валідація конфіга
            var middle = kept.Count / 2;

            return kept.Count % 2 == 1 ? kept[middle] : (kept[middle - 1] + kept[middle]) / 2;
        }

        /// <summary>
        /// Коридор для категорії. Медіана притискається до якоря в межах
        /// [MedianMinAnchorShare; MedianMaxAnchorShare] — так дві змовлені
        /// сторони не зсунуть ринок далі, ніж на цю межу.
        /// </summary>
        /// <returns>null — у категорії немає якоря, торгувати не можна.</returns>
        public PriceCorridor? Corridor(string pricingKey, double? median)
        {
            if (AnchorPerUnit(pricingKey) is not { } anchor)
                return null;

            var basis = median is { } value
                ? Math.Clamp(value, anchor * _market.MedianMinAnchorShare, anchor * _market.MedianMaxAnchorShare)
                : anchor;

            return new PriceCorridor(basis * (1 - _market.CorridorShare), basis * (1 + _market.CorridorShare));
        }

        /// <summary>Податок на виставлення: частка ціни, не менше одного золота.</summary>
        public int ListingTax(int price) => Math.Max(1, (int)Math.Ceiling(price * _market.ListingTaxShare));

        /// <summary>Скільки лотів одночасно дозволяє ринок цього рівня.</summary>
        public int ListingLimit(int marketLevel)
            => _market.BaseListings + _market.ListingsPerBuildingLevel * Math.Max(0, marketLevel);
    }
}
