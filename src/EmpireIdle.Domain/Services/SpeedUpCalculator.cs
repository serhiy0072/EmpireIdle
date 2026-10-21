using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Скільки таймера зріже прискорення і скільки воно коштує в gems.
    /// Межа своя для кожного таймера (рішення 08.10.2026); з 19.10.2026 марш теж без межі —
    /// його прискорюють повторно, кожного разу від залишку (10 хв → 1 хв → 6 с).
    /// Безкоштовного фінішу немає — будь-яке прискорення платне.
    /// </summary>
    public class SpeedUpCalculator
    {
        private readonly MonetizationConfig _config;

        public SpeedUpCalculator(MonetizationConfig config)
        {
            _config = config;
        }

        /// <summary>Межа, нижче якої прискорення таймер не зводить; немає в конфігу — нуль.</summary>
        public TimeSpan Floor(SpeedUpTimer timer)
            => TimeSpan.FromSeconds(_config.SpeedUpFloorSeconds.GetValueOrDefault(timer));

        /// <summary>Скільки таймера зрізає прискорення: усе понад межу. Zero — прискорювати нічого.</summary>
        public TimeSpan GetCut(SpeedUpTimer timer, DateTime completesAt, DateTime now)
        {
            var cut = completesAt - now - Floor(timer);

            return cut > TimeSpan.Zero ? cut : TimeSpan.Zero;
        }

        /// <summary>
        /// Те саме, що GetCut, для команди прискорення: коли зрізати нічого,
        /// це відмова гравцю, а не безкоштовна покупка.
        /// </summary>
        public TimeSpan RequireCut(SpeedUpTimer timer, DateTime completesAt, DateTime now)
        {
            var cut = GetCut(timer, completesAt, now);

            if (cut <= TimeSpan.Zero)
            {
                var floorSeconds = (int)Floor(timer).TotalSeconds;

                throw new InvalidStateException(RefusalReasons.SpeedUpAtFloor,
                    $"Less than {floorSeconds} s remain; there is nothing to speed up.", floorSeconds);
            }

            return cut;
        }

        /// <summary>Варіант прискорення маршу за відсотком; null — такого варіанта в конфізі немає.</summary>
        public MarchSpeedUpConfig? MarchOption(int percent)
            => _config.MarchSpeedUps.FirstOrDefault(option => option.Percent == percent);

        /// <summary>
        /// Скільки дороги зрізає прискорення маршу на percent відсотків залишку. Межа маршу з конфіга
        /// (якщо задана) не зрізається, і коли зрізати нічого — це відмова, а не безкоштовна покупка.
        /// </summary>
        public TimeSpan RequireMarchCut(int percent, DateTime arrivesAt, DateTime now)
        {
            var remaining = arrivesAt - now;
            var left = remaining * (1 - percent / 100.0);
            var floor = Floor(SpeedUpTimer.March);
            var cut = remaining - (left > floor ? left : floor);

            if (cut <= TimeSpan.Zero)
            {
                var floorSeconds = (int)floor.TotalSeconds;

                throw new InvalidStateException(RefusalReasons.SpeedUpAtFloor,
                    $"Less than {floorSeconds} s remain; there is nothing to speed up.", floorSeconds);
            }

            return cut;
        }

        /// <summary>
        /// Ціна прискорення в gems за зрізану частину. Крива сублінійна: подвоєння часу
        /// дає приблизно +68% ціни, тож довгі таймери лишаються в межах одного пакета.
        /// 0 — лише коли прискорювати нічого; інакше щонайменше 1 gem.
        /// </summary>
        public int GetCost(SpeedUpTimer timer, DateTime completesAt, DateTime now)
        {
            var cut = GetCut(timer, completesAt, now);

            if (cut <= TimeSpan.Zero)
                return 0;

            return Math.Max(1, (int)Math.Ceiling(_config.SpeedUpFactor * Math.Pow(cut.TotalMinutes, _config.SpeedUpExponent)));
        }
    }
}
