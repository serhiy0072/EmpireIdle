using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Що саме змінилось на артефакті за один ролл.</summary>
    /// <param name="Added">Нові стати з їхніми значеннями.</param>
    /// <param name="Raised">Приріст до наявних статів.</param>
    public record ArtifactRoll(
        IReadOnlyDictionary<string, double> Added,
        IReadOnlyDictionary<string, double> Raised);

    /// <summary>
    /// Розігрує стати артефактів.
    ///
    /// Увесь випадок бере з сіда через DeterministicRandom, а не з
    /// IRandomSource напряму: сід зберігається поруч із предметом, і будь-який
    /// ролл можна переграти. Скарга «прокачав тричі й усе в сміттєвий стат»
    /// інакше не має відповіді, а рандом у прогресії предмета — рівно той
    /// випадок, коли такі скарги будуть.
    /// </summary>
    public class ArtifactRoller
    {
        private readonly EquipmentConfig _config;

        public ArtifactRoller(EquipmentConfig config)
        {
            _config = config;
        }

        /// <summary>Стартовий набір: два випадкові стати з пулу.</summary>
        /// <param name="setKey">SetKey предмета: задає рівень і характер набору; null — без них.</param>
        public IReadOnlyDictionary<string, double> RollInitial(Rarity rarity, string? setKey, int seed)
        {
            var random = new DeterministicRandom(seed);
            var set = _config.FindArtifactSet(setKey);
            var pool = _config.ArtifactStats.OrderBy(s => s.Stat, StringComparer.Ordinal).ToList();
            var result = new Dictionary<string, double>();

            for (var i = 0; i < _config.ArtifactBaseStats && pool.Count > 0; i++)
            {
                var pick = Pick(pool, set, random);
                pool.Remove(pick);

                result[pick.Stat] = Value(pick.Min, pick.Max, rarity, set, random);
            }

            return result;
        }

        /// <summary>
        /// Що дає перехід на заданий рівень. Рівні, яких немає ні в
        /// ArtifactStatLevels, ні в ArtifactUpgradeLevels, не дають нічого:
        /// прокачка все одно коштує золота, але міняє лише число на предметі.
        /// </summary>
        public ArtifactRoll RollForLevel(int level, Rarity rarity, string? setKey,
            IReadOnlyCollection<string> currentStats, int seed)
        {
            var random = new DeterministicRandom(seed);
            var set = _config.FindArtifactSet(setKey);
            var added = new Dictionary<string, double>();
            var raised = new Dictionary<string, double>();

            if (_config.ArtifactStatLevels.Contains(level))
            {
                var pool = _config.ArtifactStats
                    .Where(s => !currentStats.Contains(s.Stat))
                    .OrderBy(s => s.Stat, StringComparer.Ordinal)
                    .ToList();

                // Пул вичерпано — новий стат не з'явиться; прокачка тоді
                // просто не додає нічого, і це не помилка
                if (pool.Count > 0)
                {
                    var pick = Pick(pool, set, random);
                    added[pick.Stat] = Value(pick.Min, pick.Max, rarity, set, random);
                }
            }

            if (_config.ArtifactUpgradeLevels.Contains(level) && currentStats.Count > 0)
            {
                // Скільки статів качаємо, вирішується першим кидком — до того,
                // як обрано які саме: інакше шанс залежав би від порядку
                var count = random.NextDouble() < _config.DoubleUpgradeChance ? 2 : 1;

                var candidates = currentStats.OrderBy(s => s, StringComparer.Ordinal).ToList();

                for (var i = 0; i < count && candidates.Count > 0; i++)
                {
                    var stat = candidates[random.Next(candidates.Count)];
                    candidates.Remove(stat);

                    var band = _config.ArtifactStats.FirstOrDefault(s => s.Stat == stat);

                    if (band is null)
                        continue;

                    raised[stat] = Value(band.UpgradeMin, band.UpgradeMax, rarity, set, random);
                }
            }

            return new ArtifactRoll(added, raised);
        }

        /// <summary>
        /// Вибір стату з пулу. Без характеру — рівноймовірно й тим самим кидком,
        /// що й до появи характерів: журнали ролів старих предметів відтворюються.
        /// З характером — характерні стати важать ArtifactFocusWeight.
        /// </summary>
        private ArtifactStatConfig Pick(List<ArtifactStatConfig> pool, ArtifactSetConfig? set, DeterministicRandom random)
        {
            if (set is null || set.FocusStats.Count == 0)
                return pool[random.Next(pool.Count)];

            var weights = pool
                .Select(s => set.FocusStats.Contains(s.Stat) ? _config.ArtifactFocusWeight : 1.0)
                .ToList();

            var roll = random.NextDouble() * weights.Sum();

            for (var i = 0; i < pool.Count; i++)
            {
                roll -= weights[i];

                if (roll < 0)
                    return pool[i];
            }

            // Похибка double на останньому кроці — беремо останній
            return pool[^1];
        }

        private double Value(double min, double max, Rarity rarity, ArtifactSetConfig? set, DeterministicRandom random)
        {
            var multiplier = _config.ArtifactRarityMultipliers.GetValueOrDefault(rarity.ToString(), 1.0)
                * TierMultiplier(set);

            return Math.Round((min + random.NextDouble() * (max - min)) * multiplier, 2);
        }

        /// <summary>Рівень поза списком множників бере останній відомий — як і тір героя.</summary>
        private double TierMultiplier(ArtifactSetConfig? set)
        {
            var multipliers = _config.ArtifactTierMultipliers;

            if (set is null || multipliers.Count == 0)
                return 1.0;

            return multipliers[Math.Clamp(set.Tier - 1, 0, multipliers.Count - 1)];
        }
    }
}
