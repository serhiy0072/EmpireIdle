using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Рахує вартість прискорення таймерів у gems.</summary>
    public class SpeedUpCalculator
    {
        private readonly MonetizationConfig _config;

        public SpeedUpCalculator(MonetizationConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Ціна прискорення в gems. Крива сублінійна: подвоєння часу
        /// дає приблизно +68% ціни, тож довгі таймери лишаються в межах
        /// одного пакета, а короткі майже безкоштовні.
        /// </summary>
        public int GetInstantFinishCost(DateTime completesAt, DateTime now)
        {
            var remaining = completesAt - now;

            if (remaining <= TimeSpan.Zero)
                return 0;

            var minutes = remaining.TotalMinutes;

            if (minutes <= _config.InstantFinishThresholdMinutes)
                return 0;

            var cost = (int)Math.Ceiling(_config.SpeedUpFactor * Math.Pow(minutes, _config.SpeedUpExponent));

            return cost;
        }
    }
}
