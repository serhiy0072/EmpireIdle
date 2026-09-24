using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Запобіжники падіння міста (GDD §2.6), крім щита: той перевіряється
    /// раніше, бо щит не пускає до бою взагалі. Чиста функція від сил і
    /// лічильника — без неї механіку не запускати.
    /// </summary>
    public sealed class CityFallRules
    {
        private readonly CityFallConfig _config;

        public CityFallRules(GameCatalog catalog) => _config = catalog.Config.Combat.CityFall;

        /// <summary>Чи діє механіка у світі — щоб не питати сили, коли вона вимкнена.</summary>
        public bool IsEnabled => _config.Enabled;

        /// <summary>Скільки часу діє щит після падіння.</summary>
        public TimeSpan ShieldDuration => TimeSpan.FromHours(_config.ShieldHours);

        /// <summary>Початок вікна, в якому рахуються виселення нападника.</summary>
        public DateTime WindowStart(DateTime utcNow) => utcNow - TimeSpan.FromHours(_config.EvictionWindowHours);

        /// <summary>
        /// Чи виселяє переможний бій. Нульова сила захисника виселення не дає:
        /// порівнювати немає з чим, а село без війська — саме той випадок,
        /// коли виселення було б чистим griefing.
        /// </summary>
        public CityFallVerdict Judge(double attackerPower, double defenderPower, int recentEvictions)
        {
            if (!_config.Enabled)
                return CityFallVerdict.Disabled;

            if (defenderPower <= 0 || attackerPower > _config.MaxPowerRatio * defenderPower)
                return CityFallVerdict.AttackerTooStrong;

            if (recentEvictions >= _config.EvictionsPerAttacker)
                return CityFallVerdict.LimitReached;

            return CityFallVerdict.Evict;
        }
    }
}
