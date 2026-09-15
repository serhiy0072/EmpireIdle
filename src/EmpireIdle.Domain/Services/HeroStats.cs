using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Підсумкові стати героя: власні плюс спорядження.
    ///
    /// Порядок навмисний і незворотний: спершу рівень і тір дають стат героя,
    /// і вже до готового числа додається спорядження. Множник тіру на екіп
    /// не діє — інакше той самий меч на третьому тірі коштував би вдвічі
    /// більше, ніж на першому, і сенс шукати кращий зникав би.
    ///
    /// Чиста функція: нічого не зберігає, нічого не перераховує наперед.
    /// </summary>
    public class HeroStats
    {
        private readonly HeroProgression _progression;
        private readonly GameCatalog _catalog;

        public HeroStats(HeroProgression progression, GameCatalog catalog)
        {
            _progression = progression;
            _catalog = catalog;
        }

        /// <summary>
        /// Усі стати героя з урахуванням вдягненого.
        /// </summary>
        /// <param name="equipped">Спорядження саме цього героя.</param>
        public IReadOnlyDictionary<string, double> Compute(Hero hero, HeroConfig config,
            IReadOnlyCollection<EquipmentItem> equipped)
        {
            var result = new Dictionary<string, double>();

            foreach (var stat in config.BaseStats.Keys)
                result[stat] = _progression.StatValue(config, stat, hero.Level, hero.Tier);

            foreach (var item in equipped)
            {
                foreach (var stat in item.Stats)
                {
                    var value = item.GetStatValue(stat.StatKey, _catalog.Config.Equipment.EnhancementBonusPerLevel);

                    result[stat.StatKey] = result.GetValueOrDefault(stat.StatKey) + value;
                }
            }

            foreach (var (stat, value) in SetBonus(equipped))
                result[stat] = result.GetValueOrDefault(stat) + value;

            return result;
        }

        /// <summary>
        /// Бонуси за повні набори. Зламане не рахується: воно не дає й
        /// власних статів, тож і комплект ним не закривається.
        /// </summary>
        public IReadOnlyDictionary<string, double> SetBonus(IReadOnlyCollection<EquipmentItem> equipped)
        {
            var result = new Dictionary<string, double>();
            var equipment = _catalog.Config.Equipment;

            if (equipment.SetBonuses.Count == 0)
                return result;

            var worn = equipped
                .Where(e => !e.IsBroken)
                .Select(e => e.ItemKey)
                .ToList();

            foreach (var bonus in equipment.SetBonuses)
            {
                var pieces = _catalog.SetPieces.GetValueOrDefault(bonus.SetKey, []);

                var count = worn.Count(pieces.Contains);

                if (count < bonus.RequiredPieces)
                    continue;

                foreach (var (stat, value) in bonus.Stats)
                    result[stat] = result.GetValueOrDefault(stat) + value;
            }

            return result;
        }
    }
}
