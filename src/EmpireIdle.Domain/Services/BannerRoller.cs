using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Стан pity гравця в групі банерів. Два лічильники, бо гарантії дві
    /// й вони незалежні: рідкісний кожні RarePity, унікальний кожні UniquePity.
    /// </summary>
    /// <param name="FeaturedGuaranteed">
    /// Програний 50/50: наступний унікальний буде банерним без кидка.
    /// </param>
    public readonly record struct PityState(int RareSince, int UniqueSince, bool FeaturedGuaranteed)
    {
        public static PityState Empty => new(0, 0, false);
    }

    /// <summary>Результат одного ролла разом із новим станом pity.</summary>
    public record BannerRollResult(BannerDropConfig Drop, PityState State, bool WasPity, bool LostFiftyFifty);

    /// <summary>
    /// Розігрує банер. Чиста функція від конфіга, стану pity й сіда:
    /// у журнал лягає сід, і скаргу «крутив 90 разів» можна переграти
    /// точно так само, як бій (§5.8).
    /// </summary>
    public class BannerRoller
    {
        /// <summary>Банерний лот або стандартний — рівно два результати.</summary>
        private const int FiftyFiftyOutcomes = 2;

        private readonly ShopConfig _config;

        public BannerRoller(ShopConfig config)
        {
            _config = config;
        }

        /// <summary>Банер за ключем від гравця. null означає «такого немає».</summary>
        public BannerConfig? FindBanner(string key)
            => _config.Banners.FirstOrDefault(b => b.Key == key);

        /// <summary>
        /// Чи лот належить до категорії банера — тобто рухає гарантії й може бути
        /// промо. На стандартному банері категорія — і герої, і зброя; філер там
        /// лише те, що не є ні тим, ні іншим.
        /// </summary>
        public static bool Counts(BannerConfig banner, BannerDropConfig drop)
            => banner.Kind == BannerKind.Standard
                ? drop.Kind is BannerKind.Hero or BannerKind.Weapon
                : drop.Kind == banner.Kind;

        public BannerRollResult Roll(BannerConfig banner, PityState state, int seed)
        {
            var random = new DeterministicRandom(seed);

            // Порівняння з +1: цей ролл і є тим, на якому гарантія спрацьовує
            var uniquePity = state.UniqueSince + 1 >= banner.UniquePity;
            var rarePity = state.RareSince + 1 >= banner.RarePity;

            // Гарантія платить у категорії банера. Звичайний лут у пул гарантій не входить
            var pool = uniquePity || rarePity
                ? banner.Drops.Where(d => Counts(banner, d)
                    && d.Rarity >= (uniquePity ? Rarity.Unique : Rarity.Rare))
                : banner.Drops;

            var drop = Pick(banner, pool, random);
            var lost = false;

            if (Counts(banner, drop) && drop.Rarity == Rarity.Unique && banner.FeaturedKey is { } featuredKey)
            {
                var featured = banner.Drops.FirstOrDefault(d => d.Key == featuredKey)
                    ?? throw new InvalidOperationException(
                        $"Banner '{banner.Key}' points at a featured drop '{featuredKey}' that is not in its pool.");

                // Програш 50/50 можливий лише тоді, коли банерний лот не єдиний
                // унікальний своєї категорії: інакше «випадковий стандартний» брати нізвідки
                var alternatives = banner.Drops
                    .Where(d => Counts(banner, d) && d.Rarity == Rarity.Unique && d.Key != featuredKey)
                    .OrderBy(d => d.Key, StringComparer.Ordinal)
                    .ToList();

                if (state.FeaturedGuaranteed || alternatives.Count == 0 || random.Next(FiftyFiftyOutcomes) == 0)
                {
                    drop = featured;
                }
                else
                {
                    drop = alternatives[random.Next(alternatives.Count)];
                    lost = true;
                }
            }

            // Лічильники рухає лише лот своєї категорії: унікальна зброя філером
            // на банері героїв не має закривати героїчну гарантію
            var ofKind = Counts(banner, drop);

            var next = new PityState(
                RareSince: ofKind && drop.Rarity >= Rarity.Rare ? 0 : state.RareSince + 1,
                UniqueSince: ofKind && drop.Rarity == Rarity.Unique ? 0 : state.UniqueSince + 1,
                FeaturedGuaranteed: ofKind && drop.Rarity == Rarity.Unique ? lost : state.FeaturedGuaranteed);

            return new BannerRollResult(drop, next, WasPity: uniquePity || rarePity, LostFiftyFifty: lost);
        }

        /// <summary>
        /// Базові шанси у відсотках — без pity, як їх показує магазин.
        /// Розкриття обов'язкове за правилами сторів (§6.2).
        /// </summary>
        public Dictionary<string, double> Odds(BannerConfig banner)
        {
            var total = banner.Drops.Sum(d => d.Weight);

            return banner.Drops.ToDictionary(
                d => d.Key,
                d => total > 0 ? Math.Round(d.Weight * 100.0 / total, 2) : 0);
        }

        /// <summary>
        /// Зважений вибір серед лотів не нижче за задану рідкість.
        /// Сортування за ключем — щоб порядок у JSON не впливав на результат сіда.
        /// </summary>
        private static BannerDropConfig Pick(BannerConfig banner, IEnumerable<BannerDropConfig> pool, IRandomSource random)
        {
            var ordered = pool.OrderBy(d => d.Key, StringComparer.Ordinal).ToList();

            if (ordered.Count == 0)
                throw new InvalidOperationException($"Banner '{banner.Key}' has an empty pool for a guaranteed roll.");

            var total = ordered.Sum(d => d.Weight);

            if (total <= 0)
                throw new InvalidOperationException($"Banner '{banner.Key}' has no drop with positive weight.");

            var roll = random.Next(total);
            var cumulative = 0;

            foreach (var drop in ordered)
            {
                cumulative += drop.Weight;

                if (roll < cumulative)
                    return drop;
            }

            return ordered[^1]; // недосяжно: roll < total
        }
    }
}
