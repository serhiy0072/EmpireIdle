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
        private readonly ArtifactStats _artifacts;

        public HeroStats(HeroProgression progression, GameCatalog catalog)
        {
            _progression = progression;
            _catalog = catalog;
            _artifacts = new ArtifactStats(catalog);
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
                result[stat] = _progression.StatValue(config, stat, hero.EffectiveLevel, hero.Tier, hero.NativeTier, hero.StarParts, hero.WeaponLevel);

            var multiplier = _artifacts.ClassMultiplier(config.Class);

            foreach (var item in equipped)
            {
                foreach (var (stat, value) in _artifacts.Compute(item, multiplier))
                    result[stat] = result.GetValueOrDefault(stat) + value;
            }

            foreach (var (stat, value) in SetBonus(equipped))
                result[stat] = result.GetValueOrDefault(stat) + value;

            // Відсотки героя — останніми: множать увесь стат, разом із наборами й базою предметів
            foreach (var (percent, stat) in ArtifactStats.HeroPercentStats)
            {
                if (result.TryGetValue(percent, out var bonus) && result.ContainsKey(stat))
                    result[stat] *= 1 + bonus / 100;
            }

            return result;
        }

        /// <summary>
        /// Герої від найсильнішого (GDD §6.1): перший — обличчя маршу у звітах і на карті й першим
        /// бере вільний слот лідера, коли марш прибуває. За рівної сили — хто прийшов раніше,
        /// щоб порядок не стрибав. Герой, якого вже немає в довіднику, — найслабший.
        /// </summary>
        public List<Hero> StrongestFirst(IEnumerable<Hero> heroes)
            => heroes
                .OrderByDescending(h => _catalog.FindHero(h.HeroKey) is { } config ? Power(h, config) : 0)
                .ThenBy(h => h.AcquiredAt)
                .ThenBy(h => h.Id)
                .ToList();

        /// <summary>
        /// Сила героя без спорядження: сума власних статів від рівня й тіру.
        /// Окремо від Compute, бо і рейтинг, і ринок оцінюють героя без того,
        /// що на ньому вдягнено, — екіп живе своїм життям і продається окремо.
        /// </summary>
        public double Power(Hero hero, HeroConfig config)
            => config.BaseStats.Keys.Sum(stat => _progression.StatValue(config, stat, hero.EffectiveLevel, hero.Tier, hero.NativeTier, hero.StarParts, hero.WeaponLevel));

        /// <summary>
        /// Сила героя з вдягненим (GDD §9.12): власна плюс сила кожного артефакта з множником
        /// класу героя плюс бонуси повних наборів. Сума статів із Compute тут не годиться:
        /// відсотки крита чи кулдауну не можна складати з пласкою атакою.
        /// </summary>
        public double Power(Hero hero, HeroConfig config, IReadOnlyCollection<EquipmentItem> equipped)
        {
            var multiplier = _artifacts.ClassMultiplier(config.Class);

            return Power(hero, config)
                + equipped.Sum(item => _artifacts.Power(item, multiplier))
                + SetBonus(equipped).Values.Sum();
        }

        /// <summary>Сила предмета самого по собі, без героя: так його оцінює ринок.</summary>
        public double Power(EquipmentItem item) => _artifacts.Power(item);

        /// <summary>
        /// Бонуси за повні набори з вдягненого.
        /// </summary>
        public IReadOnlyDictionary<string, double> SetBonus(IReadOnlyCollection<EquipmentItem> equipped)
        {
            var result = new Dictionary<string, double>();
            var equipment = _catalog.Config.Equipment;

            if (equipment.SetBonuses.Count == 0)
                return result;

            var worn = equipped
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
