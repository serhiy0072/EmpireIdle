using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Стати й сила артефакта (GDD §9.12).
    ///
    /// База — пласкі атака й захист героя та юнітів: рідкість, рівень і тір набору, підсилені
    /// сталими бонусами заточки. Випадкові бонуси заточки — відсотки, що діють на героя.
    /// Нічого з бази не зберігається на предметі: рахується щоразу з конфігу.
    ///
    /// Чиста функція: нічого не зберігає.
    /// </summary>
    public class ArtifactStats
    {
        public const string Attack = "Attack";
        public const string Defense = "Defense";
        public const string UnitAttack = "UnitAttack";
        public const string UnitDefense = "UnitDefense";

        /// <summary>Ключі пласкої бази; лише їх можуть підсилювати сталі бонуси слота.</summary>
        public static readonly IReadOnlySet<string> BaseStats = new HashSet<string> { Attack, Defense, UnitAttack, UnitDefense };

        /// <summary>
        /// Відсоткові бонуси, що множать готовий стат героя: AttackPercent підсилює
        /// всю атаку героя, а не лише ту, що дав предмет.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> HeroPercentStats = new Dictionary<string, string>
        {
            ["AttackPercent"] = "Attack",
            ["DefensePercent"] = "Defense",
            ["HealthPercent"] = "Health",
        };

        private readonly GameCatalog _catalog;

        public ArtifactStats(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>Множник класу героя; клас без запису не змінює базу.</summary>
        public ArtifactClassMultiplierConfig? ClassMultiplier(string heroClass)
            => _catalog.Config.Equipment.ArtifactClassMultipliers.GetValueOrDefault(heroClass);

        /// <summary>
        /// Стати предмета: пласка база (Attack, Defense, UnitAttack, UnitDefense) і відсотки
        /// випадкових бонусів під своїми ключами.
        /// </summary>
        /// <param name="multiplier">Множник класу героя, що носить предмет; null — предмет сам по собі.</param>
        public IReadOnlyDictionary<string, double> Compute(EquipmentItem item, ArtifactClassMultiplierConfig? multiplier = null)
        {
            var equipment = _catalog.Config.Equipment;
            var result = new Dictionary<string, double>();

            if (equipment.ArtifactBase.TryGetValue(item.Rarity, out var basis))
            {
                var tier = equipment.TierMultiplier(_catalog.FindItem(item.ItemKey)?.SetKey);
                var hero = (basis.Hero + basis.HeroPerLevel * item.Level) * tier;
                var unit = (basis.Unit + basis.UnitPerLevel * item.Level) * tier;
                var attack = multiplier?.Attack ?? 1.0;
                var defense = multiplier?.Defense ?? 1.0;

                result[Attack] = Round(hero * attack * Boost(item, Attack));
                result[Defense] = Round(hero * defense * Boost(item, Defense));
                result[UnitAttack] = Round(unit * attack * Boost(item, UnitAttack));
                result[UnitDefense] = Round(unit * defense * Boost(item, UnitDefense));
            }

            foreach (var stat in item.Stats.Where(s => !BaseStats.Contains(s.StatKey)))
                result[stat.StatKey] = stat.Value;

            return result;
        }

        /// <summary>
        /// Сила предмета: пласка база за вагами героя й юнітів плюс випадкові бонуси за
        /// PowerPerPercent. Сталі бонуси окремої сили не мають — вони вже в базі.
        /// </summary>
        /// <param name="multiplier">Множник класу: сила на герої рахується з ним, сила предмета на ринку — без.</param>
        public double Power(EquipmentItem item, ArtifactClassMultiplierConfig? multiplier = null)
        {
            var equipment = _catalog.Config.Equipment;
            var power = 0.0;

            foreach (var (stat, value) in Compute(item, multiplier))
            {
                power += stat switch
                {
                    Attack or Defense => value * equipment.HeroStatPower,
                    UnitAttack or UnitDefense => value * equipment.UnitStatPower,
                    _ => value * (equipment.FindArtifactBonus(stat)?.PowerPerPercent ?? 0),
                };
            }

            return Round(power);
        }

        /// <summary>Сталий бонус заточки до базового стату: 1 + відсоток / 100.</summary>
        private static double Boost(EquipmentItem item, string stat)
            => 1 + (item.Stats.FirstOrDefault(s => s.StatKey == stat)?.Value ?? 0) / 100;

        private static double Round(double value) => Math.Round(value, 2);
    }
}
