using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>
    /// Ділить бойові втрати на поранених (лікуються в Госпіталі),
    /// миттєво відновлюваних і безповоротних.
    /// </summary>
    public class CasualtySplitter
    {
        private readonly CombatConfig _config;

        public CasualtySplitter(CombatConfig config)
        {
            _config = config;
        }

        /// <param name="seed">Сід розподілу — робить розкладку втрат відтворюваною.</param>
        public CasualtySplit Split(IReadOnlyDictionary<UnitStackKey, int> losses, int woundedCapacity, int seed)
        {
            var wounded = new Dictionary<UnitStackKey, int>();
            var recoverable = new Dictionary<UnitStackKey, int>();
            var dead = new Dictionary<UnitStackKey, int>();

            // Власний PRNG з тієї самої причини, що й у CombatCalculator:
            // послідовність Random не гарантована між версіями рантайму,
            // а зберігається лише сід.
            var random = new DeterministicRandom(seed);
            var remainingCapacity = Math.Max(0, woundedCapacity);

            // Порядок обходу Dictionary не визначений специфікацією, а кожен тип
            // тягне свій кидок — без сортування переграш міг би роздати ті самі
            // числа іншим типам і змінити розкладку поранених. Ordinal, як у Spread.
            foreach (var (stack, lost) in losses.OrderBy(pair => pair.Key.UnitType, StringComparer.Ordinal).ThenBy(pair => pair.Key.Level))
            {
                if(lost<=0)
                    continue;

                // Частка поранених — випадкова в межах конфіга
                var woundedShare = _config.WoundedShareMin + random.NextDouble() * (_config.WoundedShareMax - _config.WoundedShareMin);
                var woundedCount = (int)Math.Round(lost * woundedShare);
                var recoverableCount = (int)Math.Round(lost * _config.RecoverableShare);

                // Госпіталь не безмежний: скільки не влізло — гине
                var admitted = Math.Min(woundedCount, remainingCapacity);
                remainingCapacity -= admitted;

                var deadCount = lost - admitted - recoverableCount;
                if (deadCount < 0)
                {
                    recoverableCount += deadCount; // зменшуємо миттєві втрати, якщо поранених більше, ніж залишилося
                    deadCount = 0;
                }

                if (admitted > 0)
                    wounded[stack] = admitted;
                if(recoverableCount > 0)
                    recoverable[stack] = recoverableCount;
                if(deadCount > 0)
                    dead[stack] = deadCount;
            }

            return new CasualtySplit(wounded, recoverable, dead);
        }
    }
}
