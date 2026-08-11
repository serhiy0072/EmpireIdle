using System;
using System.Collections.Generic;
using System.Text;

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

        /// <summary>
        /// Розподіляє втрати по кошиках.
        /// </summary>
        /// <param name="losses">Загальні втрати (тип → кількість).</param>
        /// <param name="woundedCapacity">Вільна місткість Госпіталю; надлишок поранених гине.</param>
        public CasualtySplit Split(IReadOnlyDictionary<string, int> losses, int woundedCapacity)
        {
            var wounded = new Dictionary<string, int>();
            var recoverable = new Dictionary<string, int>();
            var dead = new Dictionary<string, int>();

            var random = Random.Shared;
            var remainingCapacity = Math.Max(0, woundedCapacity);
            foreach (var(unitType, lost) in losses)
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
