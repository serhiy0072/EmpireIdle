using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Technologies
{
    /// <summary>
    /// Кланові технології (рішення 28.10.2026): дерево, очки підношень на рівень, ціна спроби
    /// ресурсом, ціна gems-підношення за добу і кланові монети з випадковим множником.
    /// </summary>
    public class ClanTechnologies
    {
        private readonly ClanTechnologiesConfig _config;

        public ClanTechnologies(ClanTechnologiesConfig config)
        {
            _config = config;
            Tree = new TechnologyTree(config.Tiers, config.Nodes);
        }

        public TechnologyTree Tree { get; }

        public ClanOfferingConfig Offering => _config.Offering;

        public ClanShopConfig Shop => _config.Shop;

        /// <summary>Очки рівня: база × множник тіру × ріст ряду^(ряд−1) × ріст рівня^(рівень−1), з округленням.</summary>
        public int Points(int tier, TechnologyNodeConfig node, int level)
        {
            var tierConfig = Tree.Tier(tier)
                ?? throw new ArgumentOutOfRangeException(nameof(tier), tier, "No such technology tier.");
            var rounding = Math.Max(_config.PointsRounding, 1);

            var raw = _config.BasePoints
                      * tierConfig.PointsMultiplier
                      * Math.Pow(_config.RowGrowth, node.Row - 1)
                      * Math.Pow(_config.LevelGrowth, Math.Max(level, 1) - 1);

            return (int)Math.Max(rounding, Math.Round(raw / rounding) * rounding);
        }

        /// <summary>
        /// Ціна однієї спроби ресурсом: ціна за рівень ратуші × рівень ратуші. Ціна росте з гравцем,
        /// а очки однакові — так внесок великого й малого села рівноцінний. null — ресурс не приймають.
        /// </summary>
        public int? AttemptCost(string resource, int mainBuildingLevel)
            => _config.Offering.AttemptCostPerTownHall.TryGetValue(resource, out var perLevel)
                ? perLevel * Math.Max(mainBuildingLevel, 1)
                : null;

        /// <summary>Ціна n-го (від 1) gems-підношення за добу UTC: min(крок × n, стеля).</summary>
        public int GemOfferingPrice(int nth)
            => Math.Min(_config.Offering.GemPriceStep * Math.Max(nth, 1), _config.Offering.GemPriceCap);

        /// <summary>
        /// Монети за одне підношення: база × множник із таблиці ваг, потім × (1 + бонус монет клану).
        /// </summary>
        public int RollCoins(IRandomSource random, double coinBonus)
        {
            var multipliers = _config.Coins.Multipliers;
            var total = multipliers.Sum(m => m.Weight);
            var roll = random.NextDouble() * total;
            var multiplier = multipliers.Count == 0 ? 1 : multipliers[^1].Multiplier;

            foreach (var option in multipliers)
            {
                if (roll < option.Weight)
                {
                    multiplier = option.Multiplier;
                    break;
                }

                roll -= option.Weight;
            }

            return (int)Math.Round(_config.Coins.BaseAmount * multiplier * (1 + Math.Max(coinBonus, 0)));
        }
    }
}
