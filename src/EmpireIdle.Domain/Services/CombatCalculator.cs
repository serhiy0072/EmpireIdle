
namespace EmpireIdle.Domain.Services
{
    /// <summary>Результат бою.</summary>
    public record BattleResult(
        bool AttackerWon,
        double AttackerPower,
        double DefenderPower,
        Dictionary<string, int> AttackerLosses,
        Dictionary<string, int> DefenderLosses);

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

        /// <summary>
        /// Проводить бій між двома арміями на заданій місцевості.
        /// </summary>
        /// <param name="attacker">Склад атакувальника (тип → кількість).</param>
        /// <param name="defender">Склад захисника.</param>
        /// <param name="terrainType">Місцевість клітини бою.</param>
        /// <param name="attackerBonus">Додатковий множник атакувальнику (бусти).</param>
        /// <param name="defenderBonus">Додатковий множник захиснику (стіни тощо).</param>
        /// <param name="seed">
        /// Сід випадковості. Зберігається у звіті — бій можна переграти
        /// й отримати той самий результат.
        /// </param>
        public BattleResult Resolve(IReadOnlyDictionary<string, int> attacker, IReadOnlyDictionary<string, int> defender,
            string terrainType, int seed, double attackerBonus = 1.0, double defenderBonus = 1.0)
        {
            var random = new Random(seed);

            var attackerPower = CalculatePower(attacker, terrainType, isAttacker: true) * attackerBonus * RollRandom(random);
            var defenderPower = CalculatePower(defender, terrainType, isAttacker: false) * defenderBonus * RollRandom(random);

            var attackerWon = attackerPower > defenderPower;
            var total = attackerPower + defenderPower;

            // Що слабша сторона відносно суперника — то більші її втрати.
            // Переможець втрачає пропорційно силі супротивника, переможений — майже все.
            var attackerLossRatio = attackerWon
                ? Math.Min(0.9, defenderPower / total)
                : 1.0;

            var defenderLossRatio = attackerWon
                ? 1.0
                : Math.Min(0.9, attackerPower / total);

            return new BattleResult(
                attackerWon,
                attackerPower,
                defenderPower,
                ApplyLosses(attacker, attackerLossRatio),
                ApplyLosses(defender, defenderLossRatio));
        }

        /// <summary>
        /// Сила армії: сума статів загонів із терейн-модифікаторами, без випадковості.
        /// Публічний — прев'ю бою й реальний бій мусять рахувати однією формулою.
        /// </summary>
        public double CalculatePower(IReadOnlyDictionary<string, int> army, string terrainType, bool isAttacker)
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

                var modifier = GetTerrainModifier(terrainType, unitType);
                power += count * stat * modifier;
            }

            return power;
        }

        /// <summary>Множник типу юніта на місцевості (1.0 — без бонусу).</summary>
        private double GetTerrainModifier(string terrainType, string unitType)
            => _config.TerrainBonuses
                .FirstOrDefault(b => b.Terrain == terrainType && b.UnitType == unitType)
                ?.Modifier ?? 1.0;

        /// <summary>
        /// Випадковий множник ~N(1.0, sigma), обрізаний межами конфіга.
        /// Box-Muller: перетворює рівномірний розподіл на нормальний.
        /// </summary>
        private double RollRandom(Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = 1.0 - random.NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

            var value = 1.0 + normal * _config.RandomSigma;
            return Math.Clamp(value, _config.RandomMin, _config.RandomMax);
        }

        /// <summary>Розподіляє втрати по загонах пропорційно.</summary>
        private static Dictionary<string, int> ApplyLosses(IReadOnlyDictionary<string, int> army, double ratio)
            => army.ToDictionary(
                pair => pair.Key,
                pair => Math.Min(pair.Value, (int)Math.Ceiling(pair.Value * ratio)));
    }
}
