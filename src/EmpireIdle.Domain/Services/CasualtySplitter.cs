using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Три кошики втрат після бою.</summary>
    public record CasualtySplit(Dictionary<string, int> Wounded, Dictionary<string, int> Recoverable, Dictionary<string, int> Dead);

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
        public CasualtySplit Split(IReadOnlyDictionary<string, int> losses, int woundedCapacity, int seed)
        {
            var wounded = new Dictionary<string, int>();
            var recoverable = new Dictionary<string, int>();
            var dead = new Dictionary<string, int>();

            // Власний PRNG з тієї самої причини, що й у CombatCalculator:
            // послідовність Random не гарантована між версіями рантайму,
            // а зберігається лише сід.
            var random = new DeterministicRandom(seed);
            var remainingCapacity = Math.Max(0, woundedCapacity);

            // Порядок обходу Dictionary не визначений специфікацією, а кожен тип
            // тягне свій кидок — без сортування переграш міг би роздати ті самі
            // числа іншим типам і змінити розкладку поранених. Ordinal, як у Spread.
            foreach (var (unitType, lost) in losses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
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
                    wounded[unitType] = admitted;
                if(recoverableCount > 0)
                    recoverable[unitType] = recoverableCount;
                if(deadCount > 0)
                    dead[unitType] = deadCount;
            }

            return new CasualtySplit(wounded, recoverable, dead);
        }
    }
}
