using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Technologies
{
    /// <summary>
    /// Рівень вузла на конкретному тірі. Той самий вузол повторюється на кожному тірі, тож рівні
    /// зберігаються ключем «тір:вузол».
    /// </summary>
    public readonly record struct TechnologyKey(int Tier, string Node)
    {
        public override string ToString() => $"{Tier}:{Node}";

        public static TechnologyKey? Parse(string value)
        {
            var separator = value.IndexOf(':');

            return separator > 0 && int.TryParse(value[..separator], out var tier)
                ? new TechnologyKey(tier, value[(separator + 1)..])
                : null;
        }
    }

    /// <summary>Чи можна взяти наступний рівень вузла і що заважає.</summary>
    public enum TechnologyAvailability
    {
        Available,
        UnknownNode,
        Maxed,
        TierGated,
        PreviousTierIncomplete,
        PrerequisitesMissing
    }

    /// <summary>Коли перший ряд вкладки на наступному тірі відкривається.</summary>
    public enum TierUnlockRule
    {
        /// <summary>Особисті: останній ряд вкладки попереднього тіру вивчено повністю.</summary>
        LastLayerMaxed,

        /// <summary>Кланові (рішення 12.11): кожен вузол останнього ряду має хоча б рівень.</summary>
        LastLayerStarted
    }

    /// <summary>
    /// Дерево технологій (рішення 28.10.2026): блок вузлів вкладки повторюється тірами.
    /// Вузол відкривається, коли кожна передумова того ж тіру має хоча б рівень; перший ряд тіру —
    /// за правилом TierUnlockRule з останнього ряду вкладки попереднього тіру; сам тір — гейтами ратуші й світу.
    /// Спільне для особистих і кланових технологій: різняться лише ціною рівня.
    /// </summary>
    public class TechnologyTree
    {
        private readonly IReadOnlyList<TechnologyTierConfig> _tiers;
        private readonly Dictionary<string, TechnologyNodeConfig> _nodes;
        private readonly TierUnlockRule _tierUnlock;

        public TechnologyTree(IReadOnlyList<TechnologyTierConfig> tiers, IReadOnlyList<TechnologyNodeConfig> nodes,
            TierUnlockRule tierUnlock = TierUnlockRule.LastLayerMaxed)
        {
            _tiers = tiers;
            _tierUnlock = tierUnlock;
            _nodes = nodes.ToDictionary(n => n.Key);
        }

        public IReadOnlyCollection<TechnologyNodeConfig> Nodes => _nodes.Values;

        public IReadOnlyList<TechnologyTierConfig> Tiers => _tiers;

        public TechnologyNodeConfig? Node(string key) => _nodes.GetValueOrDefault(key);

        public TechnologyTierConfig? Tier(int tier) => tier >= 1 && tier <= _tiers.Count ? _tiers[tier - 1] : null;

        public static int LevelOf(IReadOnlyDictionary<string, int> levels, int tier, string node)
            => levels.GetValueOrDefault(new TechnologyKey(tier, node).ToString());

        /// <summary>Чи можна вивчати наступний рівень вузла на цьому тірі.</summary>
        /// <param name="mainBuildingLevel">Рівень ратуші гравця; для клану — int.MaxValue (гейту ратуші немає).</param>
        public TechnologyAvailability Availability(IReadOnlyDictionary<string, int> levels, int tier, string nodeKey,
            int mainBuildingLevel, int worldLevel)
        {
            if (Node(nodeKey) is not { } node || Tier(tier) is not { } tierConfig)
                return TechnologyAvailability.UnknownNode;

            if (LevelOf(levels, tier, nodeKey) >= node.Levels)
                return TechnologyAvailability.Maxed;

            if (mainBuildingLevel < tierConfig.MainBuildingLevel || worldLevel < tierConfig.WorldLevel)
                return TechnologyAvailability.TierGated;

            if (!TabOpen(levels, tier, node.Tab))
                return TechnologyAvailability.PreviousTierIncomplete;

            if (node.Requires.Any(required => LevelOf(levels, tier, required) < 1))
                return TechnologyAvailability.PrerequisitesMissing;

            return TechnologyAvailability.Available;
        }

        /// <summary>
        /// Чи відкрита вкладка на тірі: перший тір відкритий завжди, наступний — коли останній ряд вкладки
        /// попереднього тіру задовольняє правило відкриття (вивчено повністю чи хоча б рівень кожного вузла).
        /// </summary>
        public bool TabOpen(IReadOnlyDictionary<string, int> levels, int tier, string tab)
        {
            if (tier <= 1)
                return true;

            var tabNodes = _nodes.Values.Where(n => n.Tab == tab).ToList();
            if (tabNodes.Count == 0)
                return false;

            var lastRow = tabNodes.Max(n => n.Row);

            return tabNodes.Where(n => n.Row == lastRow).All(n => LevelOf(levels, tier - 1, n.Key) >=
                (_tierUnlock == TierUnlockRule.LastLayerStarted ? 1 : n.Levels));
        }

        /// <summary>Множник бонусу вузла на тірі: шкала тіру за типом вузла, без шкали — 1.</summary>
        public double Scale(int tier, TechnologyScaling scaling)
            => scaling != TechnologyScaling.None && Tier(tier) is { } config && config.Scales.TryGetValue(scaling, out var scale)
                ? scale
                : 1;

        /// <summary>
        /// Сила вивчених рівнів: рівень × сила тіру × вага шкали вузла (рішення 05.11, кожен рівень дає силу).
        /// Ключі, яких немає в дереві, пропускаються, а рівень понад стелю вузла не рахується, як і в бонусах.
        /// </summary>
        public double Power(IReadOnlyDictionary<string, int> levels, IReadOnlyDictionary<TechnologyScaling, double> weights)
        {
            var power = 0.0;

            foreach (var (key, level) in levels)
            {
                if (level <= 0 || TechnologyKey.Parse(key) is not { } parsed || Node(parsed.Node) is not { } node || Tier(parsed.Tier) is not { } tier)
                    continue;

                power += Math.Min(level, node.Levels) * tier.PowerPerLevel * weights.GetValueOrDefault(node.Scaling, 1);
            }

            return power;
        }

        /// <summary>
        /// Усі вивчені рівні як бонуси: рівень × бонус за рівень × шкала тіру. Ключі, яких немає в дереві
        /// (вузол прибрали з конфігу), пропускаються, а не ламають розрахунок.
        /// </summary>
        public TechnologyBonuses Bonuses(IReadOnlyDictionary<string, int> levels)
        {
            var bonuses = new TechnologyBonuses();

            foreach (var (key, level) in levels)
            {
                if (level <= 0 || TechnologyKey.Parse(key) is not { } parsed || Node(parsed.Node) is not { } node || Tier(parsed.Tier) is null)
                    continue;

                var counted = Math.Min(level, node.Levels);
                var scale = Scale(parsed.Tier, node.Scaling);

                foreach (var effect in node.Effects)
                    bonuses.Add(effect.Stat, effect.Target, counted * effect.PerLevel * scale);
            }

            return bonuses;
        }
    }
}
