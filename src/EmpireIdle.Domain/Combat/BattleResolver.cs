using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Проводить бій: рахує сили, визначає переможця, розподіляє втрати.
    ///
    /// Нічого не читає з БД і нічого не зберігає — чиста функція від аргументів.
    /// Саме тому той самий сід дає той самий результат, і бій можна переграти
    /// при розборі скарги.
    /// </summary>
    public class BattleResolver
    {
        private readonly CombatCalculator _combat;
        private readonly CasualtySplitter _casualties;

        public BattleResolver(CombatCalculator combat, CasualtySplitter casualties)
        {
            _combat = combat;
            _casualties = casualties;
        }

        /// <param name="seed">
        /// Сід випадковості. Іде і в розрахунок сил, і в розподіл втрат:
        /// інакше переграний бій дав би того самого переможця, але інший
        /// склад поранених і загиблих.
        /// </param>
        /// <param name="woundedCapacity">
        /// Вільних місць у Госпіталі атакувальника. Надлишок поранених гине.
        /// </param>
        public BattleOutcome Resolve(
            IReadOnlyDictionary<string, int> attackerArmy,
            IReadOnlyList<DefenceStack> defence,
            string terrainType,
            int seed,
            double attackerBonus,
            double defenderBonus,
            int woundedCapacity,
            StackBuff? attackerBuff = null,
            DefenceBuffs? defenceBuffs = null)
        {
            var battle = _combat.Resolve(attackerArmy, defence, terrainType, seed,
                attackerBonus, defenderBonus, attackerBuff, defenceBuffs);

            // Окремий сід для розподілу: якби він збігався з бойовим, послідовність
            // Random продовжилась би з місця, де її лишив CombatCalculator,
            // і зміна кількості кидків у бою тихо змінила б розкладку втрат
            var split = _casualties.Split(battle.AttackerLosses, woundedCapacity, seed ^ 0x5DEECE66);

            return new BattleOutcome(battle, split);
        }
    }
}
