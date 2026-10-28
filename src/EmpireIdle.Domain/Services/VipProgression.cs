using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// VIP-рівень акаунта (рішення 28.10.2026): росте від куплених за весь час gems за порогами з конфігу.
    /// Нагороди й витрати на рівень не впливають — лише справжні покупки.
    /// </summary>
    public class VipProgression
    {
        private readonly MonetizationConfig _config;

        public VipProgression(MonetizationConfig config)
        {
            _config = config;
        }

        /// <summary>Найвищий рівень, поріг якого вже досягнуто; 0 — жодного.</summary>
        public int Level(long purchasedGems) => _config.VipThresholds.Count(threshold => purchasedGems >= threshold);

        /// <summary>Скільки куплених gems відкриє наступний рівень; null — рівень уже найвищий.</summary>
        public long? NextThreshold(int level)
            => level >= 0 && level < _config.VipThresholds.Count ? _config.VipThresholds[level] : null;

        /// <summary>Маршів понад базові: по одному за кожні VipLevelsPerExtraMarch рівнів.</summary>
        public int ExtraMarches(int level) => Math.Max(level, 0) / Math.Max(_config.VipLevelsPerExtraMarch, 1);
    }
}
