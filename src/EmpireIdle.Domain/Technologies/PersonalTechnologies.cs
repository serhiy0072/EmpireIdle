using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Technologies
{
    /// <summary>
    /// Особисті технології Лабораторії (рішення 28.10.2026): дерево плюс ціна рівня в ресурсах і часі.
    /// Час: база × множник тіру × ріст ряду^(ряд−1) × ріст рівня^(рівень−1).
    /// Ціна ресурсу: хвилини × ціна хвилини × опорна ратуша тіру × вага ресурсу вкладки, з округленням.
    /// </summary>
    public class PersonalTechnologies
    {
        private readonly PersonalTechnologiesConfig _config;

        public PersonalTechnologies(PersonalTechnologiesConfig config)
        {
            _config = config;
            Tree = new TechnologyTree(config.Tiers, config.Nodes);
        }

        public TechnologyTree Tree { get; }

        /// <summary>Будівля, у якій ідуть дослідження; null — особисті технології вимкнено.</summary>
        public string? BuildingKey => _config.BuildingKey;

        public int MaxHelps => _config.MaxHelps;

        public double HelpShare => _config.HelpShare;

        /// <summary>Тривалість рівня до прискорень: швидкість досліджень і допомога рахуються окремо.</summary>
        public TimeSpan Duration(int tier, TechnologyNodeConfig node, int level)
            => TimeSpan.FromMinutes(Minutes(tier, node, level));

        /// <summary>Ціна рівня в ресурсах вкладки вузла.</summary>
        public List<ResourceCost> Cost(int tier, TechnologyNodeConfig node, int level)
        {
            var tierConfig = Tree.Tier(tier)
                ?? throw new ArgumentOutOfRangeException(nameof(tier), tier, "No such technology tier.");

            if (!_config.TabResources.TryGetValue(node.Tab, out var weights))
                return [];

            var perResource = Minutes(tier, node, level) * _config.CostPerMinutePerTownHall * tierConfig.ReferenceTownHall;
            var rounding = Math.Max(_config.CostRounding, 1);

            return weights
                .Select(pair => new ResourceCost
                {
                    Resource = pair.Key,
                    Amount = (int)Math.Max(rounding, Math.Round(perResource * pair.Value / rounding) * rounding)
                })
                .ToList();
        }

        private double Minutes(int tier, TechnologyNodeConfig node, int level)
        {
            var tierConfig = Tree.Tier(tier)
                ?? throw new ArgumentOutOfRangeException(nameof(tier), tier, "No such technology tier.");

            return _config.BaseMinutes
                   * tierConfig.TimeMultiplier
                   * Math.Pow(_config.RowTimeGrowth, node.Row - 1)
                   * Math.Pow(_config.LevelTimeGrowth, Math.Max(level, 1) - 1);
        }
    }
}
