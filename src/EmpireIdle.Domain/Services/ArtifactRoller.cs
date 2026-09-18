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
        public IReadOnlyDictionary<string, double> RollInitial(Rarity rarity, int seed)
        {
            var random = new DeterministicRandom(seed);
            var pool = _config.ArtifactStats.OrderBy(s => s.Stat, StringComparer.Ordinal).ToList();
            var result = new Dictionary<string, double>();

            for (var i = 0; i < _config.ArtifactBaseStats && pool.Count > 0; i++)
            {
                var pick = pool[random.Next(pool.Count)];
                pool.Remove(pick);

                result[pick.Stat] = Value(pick.Min, pick.Max, rarity, random);
            }

            return result;
        }

        /// <summary>
        /// Що дає перехід на заданий рівень. Рівні, яких немає ні в
        /// ArtifactStatLevels, ні в ArtifactUpgradeLevels, не дають нічого:
        /// прокачка все одно коштує золота, але міняє лише число на предметі.
        /// </summary>
        public ArtifactRoll RollForLevel(int level, Rarity rarity, IReadOnlyCollection<string> currentStats, int seed)
        {
            var random = new DeterministicRandom(seed);
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
                    var pick = pool[random.Next(pool.Count)];
                    added[pick.Stat] = Value(pick.Min, pick.Max, rarity, random);
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

                    raised[stat] = Value(band.UpgradeMin, band.UpgradeMax, rarity, random);
                }
            }

            return new ArtifactRoll(added, raised);
        }

        private double Value(double min, double max, Rarity rarity, DeterministicRandom random)
        {
            var multiplier = _config.ArtifactRarityMultipliers.GetValueOrDefault(rarity.ToString(), 1.0);

            return Math.Round((min + random.NextDouble() * (max - min)) * multiplier, 2);
        }
    }
}
