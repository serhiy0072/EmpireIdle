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
        /// Ціна миттєвого завершення: скільки gems коштує «зняти» решту часу.
        /// </summary>
        /// <param name="completesAt">Момент завершення.</param>
        /// <param name="utcNow">Поточний час.</param>
        public int GetInstantFinishCost(DateTime completesAt, DateTime utcNow)
        {
            var remaining = completesAt - utcNow;

            if (remaining <= TimeSpan.Zero)
                return 0;

            // Останні хвилини — безкоштовно: не змушуємо платити за дрібницю
            if (remaining.TotalMinutes <= _config.InstantFinishThresholdMinutes)
                return 0;

            var cost = (int)Math.Ceiling(remaining.TotalMinutes * _config.SpeedUpGemsPerMinute);
            return Math.Max(_config.SpeedUpMinGems, cost);
        }
    }
}