using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Скільки таймера зріже прискорення за gems і скільки воно коштує.
    /// Останні SpeedUpFloorSeconds не зрізаються ніколи: після покупки треба
    /// дочекатись межі. Безкоштовного фінішу немає — будь-яке прискорення платне.
    /// </summary>
    public class SpeedUpCalculator
    {
        private readonly MonetizationConfig _config;

        public SpeedUpCalculator(MonetizationConfig config)
        {
            _config = config;
        }

        /// <summary>Межа, нижче якої прискорення таймер не зводить.</summary>
        public TimeSpan Floor => TimeSpan.FromSeconds(_config.SpeedUpFloorSeconds);

        /// <summary>Скільки таймера зрізає прискорення: усе понад межу. Zero — прискорювати нічого.</summary>
        public TimeSpan GetCut(DateTime completesAt, DateTime now)
        {
            var cut = completesAt - now - Floor;

            return cut > TimeSpan.Zero ? cut : TimeSpan.Zero;
        }

        /// <summary>
        /// Те саме, що GetCut, для команди прискорення: коли зрізати нічого,
        /// це відмова гравцю, а не безкоштовна покупка.
        /// </summary>
        public TimeSpan RequireCut(DateTime completesAt, DateTime now)
        {
            var cut = GetCut(completesAt, now);

            if (cut <= TimeSpan.Zero)
                throw new InvalidStateException(RefusalReasons.SpeedUpAtFloor,
                    $"Less than {_config.SpeedUpFloorSeconds} s remain; there is nothing to speed up.", _config.SpeedUpFloorSeconds);

            return cut;
        }

        /// <summary>
        /// Ціна прискорення в gems за зрізану частину. Крива сублінійна: подвоєння часу
        /// дає приблизно +68% ціни, тож довгі таймери лишаються в межах одного пакета.
        /// 0 — лише коли прискорювати нічого; інакше щонайменше 1 gem.
        /// </summary>
        public int GetCost(DateTime completesAt, DateTime now)
        {
            var cut = GetCut(completesAt, now);

            if (cut <= TimeSpan.Zero)
                return 0;

            return Math.Max(1, (int)Math.Ceiling(_config.SpeedUpFactor * Math.Pow(cut.TotalMinutes, _config.SpeedUpExponent)));
        }
    }
}
