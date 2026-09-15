using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Рахує бій: сила сторін із урахуванням місцевості й випадковості,
    /// втрати пропорційні співвідношенню сил.
    /// </summary>
    public class CombatCalculator
    {
        private readonly CombatConfig _config;
        private readonly GameCatalog _catalog;


        public CombatCalculator(CombatConfig config, GameCatalog catalog)
        {
            _config = config;
            _catalog = catalog;
        }

        /// <param name="defence">
        /// Склад оборони по стеках. Не словник: у кожного власника свій
        /// лідер, і сила стека залежить від того, чий він.
        /// </param>
        /// <param name="attackerBuff">Пасивки героя, що веде марш.</param>
        /// <param name="defenceBuffs">Пасивки лідерів у гарнізоні.</param>
        public BattleResult Resolve(IReadOnlyDictionary<string, int> attacker, IReadOnlyList<DefenceStack> defence,
            string terrainType, int seed, double attackerBonus = 1.0, double defenderBonus = 1.0,
            StackBuff? attackerBuff = null, DefenceBuffs? defenceBuffs = null)
        {
            // Власний PRNG, а не Random: у звіті зберігається лише сід, і послідовність
            // BCL-класу не гарантована між версіями рантайму — переграш бою після
            // апгрейду SDK дав би інший результат, ніж оригінал.
            var random = new DeterministicRandom(seed);

            var attackerPower = CalculatePower(attacker, terrainType, isAttacker: true, attackerBuff)
                * attackerBonus * RollRandom(random);

            var defenderPower = CalculateDefencePower(defence, terrainType, defenceBuffs)
                * defenderBonus * RollRandom(random);

            var attackerWon = attackerPower > defenderPower;
            var total = attackerPower + defenderPower;

            // Втрати ростуть зі силою супротивника в межах своєї смуги.
            // Переможений більше не втрачає все: село після поразки лишається
            // з військом, а не з нулем, інакше одна програна оборона
            // викреслювала б гравця з гри на тижні
            var attackerLossRatio = LossRatio(
                attackerWon ? _config.AttackerWinLosses : _config.AttackerLossLosses,
                defenderPower, total);

            var defenderLossRatio = LossRatio(
                attackerWon ? _config.DefenderLossLosses : _config.DefenderWinLosses,
                attackerPower, total);

            // Скільки втратив кожен тип, вирішує бій; хто саме з власників
            // за це заплатив — DefenceLossAllocator уже після
            var defenderArmy = defence
                .GroupBy(s => s.UnitType)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Count));

            return new BattleResult(
                attackerWon,
                attackerPower,
                defenderPower,
                ApplyLosses(attacker, attackerLossRatio, terrainType, attackerWon),
                ApplyLosses(defenderArmy, defenderLossRatio, terrainType, !attackerWon));
        }

        /// <summary>
        /// Сила армії: сума статів загонів із терейн-модифікаторами, без випадковості.
        /// Публічний — прев'ю бою й реальний бій мусять рахувати однією формулою.
        /// </summary>
        /// <param name="buff">Пасивки героя цієї сторони; null — героя немає.</param>
        public double CalculatePower(IReadOnlyDictionary<string, int> army, string terrainType, bool isAttacker,
            StackBuff? buff = null)
        {
            var power = 0.0;

            foreach (var (unitType, count) in army)
            {
                if (!_catalog.Units.TryGetValue(unitType, out var config) || count <= 0)
                    continue;

                // Атакувальник спирається на атаку, захисник — на захист
                var stat = isAttacker
                    ? config.Stats.GetValueOrDefault("Attack", 1.0)
                    : config.Stats.GetValueOrDefault("Defense", 1.0);

                var hero = buff is null
                    ? 1.0
                    : isAttacker ? buff.Attack(unitType) : buff.Defense(unitType);

                power += count * stat * GetTerrainModifier(terrainType, unitType) * hero;
            }

            return power;
        }

        /// <summary>
        /// Сила оборони по стеках. Окремий метод, бо кожен стек множиться
        /// пасивками свого власника: звести оборону в один словник до бою
        /// означало б прикласти чужого лідера до моїх юнітів.
        /// </summary>
        public double CalculateDefencePower(IReadOnlyList<DefenceStack> stacks, string terrainType,
            DefenceBuffs? buffs = null)
        {
            var resolved = buffs ?? DefenceBuffs.None;
            var power = 0.0;

            foreach (var stack in stacks)
            {
                if (!_catalog.Units.TryGetValue(stack.UnitType, out var config) || stack.Count <= 0)
                    continue;

                var stat = config.Stats.GetValueOrDefault("Defense", 1.0);

                power += stack.Count * stat
                    * GetTerrainModifier(terrainType, stack.UnitType)
                    * resolved.For(stack.OwnerPlayerId).Defense(stack.UnitType);
            }

            return power;
        }

        /// <summary>
        /// Смуга шансів за співвідношенням сил. Та сама CalculatePower, що й у бою —
        /// друга формула означала б, що прев'ю розійдеться з реальністю
        /// на першому ж ребалансі.
        /// </summary>
        public BattleOdds EstimateOdds(double attackerPower, double defenderPower)
        {
            // Захисник без сили — бій формальність
            if (defenderPower <= 0)
                return BattleOdds.Overwhelming;

            var ratio = attackerPower / defenderPower;
            var thresholds = _config.PreviewOddsThresholds;

            for (var band = 0; band < thresholds.Count; band++)
            {
                if (ratio >= thresholds[band])
                    return (BattleOdds)band;
            }

            return (BattleOdds)thresholds.Count;
        }

        /// <summary>Множник типу юніта на місцевості (1.0 — без бонусу).</summary>
        private double GetTerrainModifier(string terrainType, string unitType)
            => _config.TerrainBonuses
                .FirstOrDefault(b => b.Terrain == terrainType && b.UnitType == unitType)
                ?.Modifier ?? 1.0;

        /// <summary>
        /// Частка втрат у межах смуги: лінійно від частки ворожої сили
        /// в загальній. Рівні сторони дають половину смуги, розгром —
        /// її край.
        /// </summary>
        private static double LossRatio(LossBand band, double enemyPower, double totalPower)
        {
            if (totalPower <= 0)
                return band.Min;

            var share = Math.Clamp(enemyPower / totalPower, 0.0, 1.0);

            return band.Min + (band.Max - band.Min) * share;
        }

        /// <summary>
        /// Випадковий множник ~N(1.0, sigma), обрізаний межами конфіга.
        /// Box-Muller: перетворює рівномірний розподіл на нормальний.
        /// </summary>
        private double RollRandom(DeterministicRandom random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            var value = 1.0 + normal * _config.RandomSigma;
            return Math.Clamp(value, _config.RandomMin, _config.RandomMax);
        }

        /// <summary>
        /// Розподіляє втрати по типах юнітів.
        ///
        /// Загальна кількість утрачених визначається співвідношенням сил
        /// і однакова для обох правил нижче. Різниця в тому, як вона
        /// лягає на типи.
        ///
        /// Переможець втрачає обернено до фактичного захисту: облога тане,
        /// піхота тримається, а найстійкіші загони при розгромній перевазі
        /// виходять узагалі без утрат. Переможений втрачає рівномірно —
        /// розгром не розбирає, хто якісніший.
        /// </summary>
        private Dictionary<string, int> ApplyLosses(
            IReadOnlyDictionary<string, int> army, double ratio, string terrainType, bool sideWon)
        {
            var result = army.ToDictionary(pair => pair.Key, _ => 0);

            var units = army.Where(pair => pair.Value > 0).ToList();

            if (units.Count == 0)
                return result;

            var totalUnits = units.Sum(pair => pair.Value);
            var totalLost = Math.Min(totalUnits, (int)Math.Round(totalUnits * ratio, MidpointRounding.AwayFromZero));

            if (totalLost <= 0)
                return result;

            // Переможений: вага дорівнює кількості, тобто рівномірна частка.
            // Переможець: обернено до захисту з терейн-модифікатором
            var weighted = units
                .Select(pair => (
                    Key: pair.Key,
                    Count: pair.Value,
                    Weight: sideWon
                        ? pair.Value / EffectiveDefence(pair.Key, terrainType)
                        : (double)pair.Value))
                .ToList();

            if (sideWon)
                weighted = DropNegligible(weighted, totalLost);

            foreach (var (key, lost) in Spread(weighted, totalLost))
                result[key] = lost;

            return result;
        }

        /// <summary>
        /// Фактичний захист юніта на цьому терейні. Мінімум обмежений знизу,
        /// щоб нульовий чи від'ємний стат у конфігу не давав ділення на нуль.
        /// </summary>
        private double EffectiveDefence(string unitType, string terrainType)
        {
            var stat = _catalog.Units.TryGetValue(unitType, out var config)
                ? config.Stats.GetValueOrDefault("Defense", 1.0)
                : 1.0;

            return Math.Max(0.1, stat * GetTerrainModifier(terrainType, unitType));
        }

        /// <summary>
        /// Обнуляє ваги типів, чия частка втрат нижча за поріг. Один прохід:
        /// після обнулення решта ділить усе між собою, і це вже може підняти
        /// когось вище порога — але другий прохід зробив би результат
        /// залежним від порядку, а не від чисел.
        /// </summary>
        private List<(string Key, int Count, double Weight)> DropNegligible(
            List<(string Key, int Count, double Weight)> weighted, int totalLost)
        {
            var totalWeight = weighted.Sum(x => x.Weight);

            if (totalWeight <= 0)
                return weighted;

            var survivors = weighted
                .Where(x => totalLost * x.Weight / totalWeight / x.Count >= _config.NoLossShareThreshold)
                .ToList();

            // Якщо поріг відсік геть усіх, втрати мають лягти хоч кудись
            return survivors.Count > 0 ? survivors : weighted;
        }

        /// <summary>
        /// Розкидає ціле число втрат за вагами: цілі частини плюс залишок
        /// за найбільшими дробовими частинами. Тип не може втратити більше,
        /// ніж мав, тож надлишок перерозподіляється між рештою.
        /// </summary>
        private static IEnumerable<(string Key, int Lost)> Spread(
            List<(string Key, int Count, double Weight)> weighted, int totalLost)
        {
            var assigned = weighted.ToDictionary(x => x.Key, _ => 0);
            var open = weighted.ToList();
            var remaining = totalLost;

            // Кожна ітерація або роздає все, або закриває хоча б один тип
            while (remaining > 0 && open.Count > 0)
            {
                var totalWeight = open.Sum(x => x.Weight);

                if (totalWeight <= 0)
                    break;

                var shares = open
                    .Select(x =>
                    {
                        var exact = remaining * x.Weight / totalWeight;

                        return (x.Key, x.Count, Whole: (int)Math.Floor(exact), Fraction: exact - Math.Floor(exact));
                    })
                    .ToList();

                var whole = shares.Sum(s => s.Whole);
                var leftover = remaining - whole;

                var queue = shares
                    .OrderByDescending(s => s.Fraction)
                    .ThenByDescending(s => s.Count)
                    .ThenBy(s => s.Key, StringComparer.Ordinal)
                    .ToList();

                for (var i = 0; i < leftover; i++)
                {
                    var item = queue[i];
                    shares[shares.FindIndex(s => s.Key == item.Key)] = (item.Key, item.Count, item.Whole + 1, 0);
                }

                remaining = 0;

                foreach (var share in shares)
                {
                    var capacity = share.Count - assigned[share.Key];
                    var take = Math.Min(share.Whole, capacity);

                    assigned[share.Key] += take;

                    // Те, що не влізло, поїде наступною ітерацією до інших типів
                    remaining += share.Whole - take;
                }

                open = open.Where(x => assigned[x.Key] < x.Count).ToList();
            }

            return assigned.Where(pair => pair.Value > 0).Select(pair => (pair.Key, pair.Value));
        }

    }
}
