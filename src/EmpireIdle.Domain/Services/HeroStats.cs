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
            => ComputeAt(hero, config, equipped, hero.EffectiveLevel);

        /// <summary>
        /// Стати героя, якби він був на заданому рівні, — решта (тір, зірки, зброя, спорядження) та сама.
        /// Так екран героя показує «було → стане» до підняття рівня.
        /// </summary>
        public IReadOnlyDictionary<string, double> ComputeAt(Hero hero, HeroConfig config,
            IReadOnlyCollection<EquipmentItem> equipped, int level)
        {
            var result = new Dictionary<string, double>();

            foreach (var stat in config.BaseStats.Keys)
                result[stat] = _progression.StatValue(config, stat, level, hero.Tier, hero.NativeTier, hero.StarParts, hero.WeaponLevel);

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
        /// Герої від найсильнішого (GDD §6.1): перший — обличчя маршу у звітах і на карті.
        /// За рівної сили — хто прийшов раніше,
        /// щоб порядок не стрибав. Герой, якого вже немає в довіднику, — найслабший.
        /// </summary>
        public List<Hero> StrongestFirst(IEnumerable<Hero> heroes)
            => heroes
                .OrderByDescending(h => _catalog.FindHero(h.HeroKey) is { } config ? Power(h, config) : 0)
                .ThenBy(h => h.AcquiredAt)
                .ThenBy(h => h.Id)
                .ToList();

        /// <summary>
        /// Сила героя без спорядження: власні стати від рівня й тіру, кожен зі своєю вагою.
        /// Окремо від Compute, бо і рейтинг, і ринок оцінюють героя без того,
        /// що на ньому вдягнено, — екіп живе своїм життям і продається окремо.
        /// </summary>
        public double Power(Hero hero, HeroConfig config)
        {
            var settings = _catalog.Config.HeroSettings;

            return config.BaseStats.Keys.Sum(stat => settings.StatPowerWeight(stat)
                * _progression.StatValue(config, stat, hero.EffectiveLevel, hero.Tier, hero.NativeTier, hero.StarParts, hero.WeaponLevel));
        }

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
                + SetPower(hero, config, equipped);
        }

        /// <summary>
        /// Сила бонусів наборів за тими самими вагами, що й решта (рішення 15.10.2026): пласкі стати —
        /// за вагою стата, відсоток до стата героя — за реальним приростом цього стата, решта — за
        /// PowerPerPercent. Сира сума складала б здоров'я з атакою один до одного й відсотки з числами.
        /// </summary>
        private double SetPower(Hero hero, HeroConfig config, IReadOnlyCollection<EquipmentItem> equipped)
        {
            var bonuses = SetBonus(equipped);

            if (bonuses.Count == 0)
                return 0;

            var settings = _catalog.Config.HeroSettings;
            var equipment = _catalog.Config.Equipment;
            var multiplier = _artifacts.ClassMultiplier(config.Class);
            var power = 0.0;

            foreach (var (stat, value) in bonuses)
            {
                if (ArtifactStats.HeroPercentStats.TryGetValue(stat, out var target))
                {
                    // Відсоток множить увесь стат: власний героя й пласкі з предметів і наборів
                    var flat = _progression.StatValue(config, target, hero.EffectiveLevel, hero.Tier, hero.NativeTier, hero.StarParts, hero.WeaponLevel)
                        + equipped.Sum(item => _artifacts.Compute(item, multiplier).GetValueOrDefault(target))
                        + bonuses.GetValueOrDefault(target);

                    power += flat * value / 100 * settings.StatPowerWeight(target) * equipment.HeroStatPower;
                }
                else if (stat is ArtifactStats.Attack or ArtifactStats.Defense or ArtifactStats.Health)
                    power += value * settings.StatPowerWeight(stat) * equipment.HeroStatPower;
                else
                    power += value * (equipment.FindArtifactBonus(stat)?.PowerPerPercent ?? 0);
            }

            return Math.Round(power, 2);
        }

        /// <summary>Сила предмета самого по собі, без героя: так його оцінює ринок.</summary>
        public double Power(EquipmentItem item) => _artifacts.Power(item);

        /// <summary>
        /// Бонуси наборів із вдягненого за порогами (рішення 15.10.2026): предмети рахуються по родині
        /// будь-якої рідкості, поріг N дає свій бонус, щойно вдягнено N предметів родини, а числа бере
        /// з рідкості N-го предмета, якщо розкласти їх від найрідкіснішого. Так 2 унікальні + 2 звичайні
        /// дають поріг 2 унікальний, а поріг 4 — звичайний; при трьох предметах діє лише поріг 2.
        /// </summary>
        public IReadOnlyDictionary<string, double> SetBonus(IReadOnlyCollection<EquipmentItem> equipped)
        {
            var result = new Dictionary<string, double>();
            var bonuses = _catalog.Config.Equipment.SetBonuses;

            if (bonuses.Count == 0)
                return result;

            var worn = equipped
                .Select(e => _catalog.FindItem(e.ItemKey)?.SetKey)
                .OfType<string>()
                .Select(EquipmentConfig.SplitSetKey)
                .GroupBy(piece => piece.Family)
                .ToDictionary(g => g.Key, g => g.Select(piece => piece.Rarity).OrderByDescending(r => r).ToList());

            foreach (var bonus in bonuses)
            {
                var (family, rarity) = EquipmentConfig.SplitSetKey(bonus.SetKey);

                if (!worn.TryGetValue(family, out var rarities) || rarities.Count < bonus.RequiredPieces
                    || rarities[bonus.RequiredPieces - 1] != rarity)
                    continue;

                foreach (var (stat, value) in bonus.Stats)
                    result[stat] = result.GetValueOrDefault(stat) + value;
            }

            return result;
        }
    }
}
