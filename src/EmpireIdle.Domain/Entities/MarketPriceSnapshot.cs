namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Знімок медіани ціни за одиницю для категорії товару (GDD §8.8).
    /// Рахує джоб зі завершених продажів; виставлення лише читає його.
    ///
    /// Знімок, а не медіана на льоту: інакше кожен продаж одразу зсував би
    /// коридор для наступного, і дві змовлені сторони розганяли б ціну
    /// серією угод за хвилину.
    /// </summary>
    public class MarketPriceSnapshot
    {
        public int ServerId { get; private set; }

        /// <summary>Категорія ціни: «weapon», «hero.Rare», «item.teleport»…</summary>
        public string PricingKey { get; private set; } = null!;

        /// <summary>Медіана за одиницю; null — продажів замало, діє якір.</summary>
        public double? MedianPerUnit { get; private set; }

        /// <summary>Скільки продажів увійшло у вікно медіани.</summary>
        public int Sales { get; private set; }

        public DateTime ComputedAt { get; private set; }

        public MarketPriceSnapshot(int serverId, string pricingKey, double? medianPerUnit, int sales, DateTime utcNow)
        {
            ServerId = serverId;
            PricingKey = pricingKey;
            Update(medianPerUnit, sales, utcNow);
        }

        protected MarketPriceSnapshot() { } // Для EF Core

        public void Update(double? medianPerUnit, int sales, DateTime utcNow)
        {
            MedianPerUnit = medianPerUnit;
            Sales = sales;
            ComputedAt = utcNow;
        }
    }
}
