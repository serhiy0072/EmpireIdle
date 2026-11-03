using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Тривалість таймерів — одна формула на запуск таймера й на все, що від неї рахує
    /// (кланова допомога скорочує частку повного часу). Копії формули вже розходились:
    /// допомога не бачила множника ранніх рівнів будівництва й суми кроків тренування,
    /// тож скорочувала менше, ніж обіцяно.
    ///
    /// Технології швидкості (рішення 28.10.2026) скорочують час як база / (1 + бонус):
    /// +50% швидкості — це дві третини часу, а не половина, тож сотня відсотків не обнуляє таймер.
    /// </summary>
    public static class TimerDurations
    {
        /// <summary>Апгрейд будівлі з поточного рівня на наступний.</summary>
        /// <param name="speedBonus">Сума технологій швидкості будівництва (0.1 — +10%).</param>
        public static TimeSpan Construction(BuildingConfig config, int currentLevel, double speedBonus = 0)
            => Faster(TimeSpan.FromMinutes(ProgressionCurves.BuildMinutes(config.BaseBuildMinutes, config.BuildTimeGrowth,
                currentLevel, config.EarlyTaxStrength, config.EarlyTaxDecay)), speedBonus);

        /// <summary>Тренування партії з нуля на рівень <paramref name="level"/> — сума кроків від 1 (§5.2).</summary>
        public static TimeSpan Training(UnitConfig unit, int level, int count, double speedBonus = 0)
            => Faster(TimeSpan.FromSeconds(
                (long)ProgressionCurves.CumulativeUnitLevelCost(unit.BaseTrainSeconds, 1, level + 1, unit.TrainTimeGrowth) * count),
                speedBonus);

        /// <summary>Прокачка партії з <paramref name="fromLevel"/> на <paramref name="toLevel"/> — лише пройдені кроки.</summary>
        public static TimeSpan LevelUp(UnitConfig unit, int fromLevel, int toLevel, int count, double speedBonus = 0)
            => Faster(TimeSpan.FromSeconds(
                (long)ProgressionCurves.CumulativeUnitLevelCost(unit.BaseTrainSeconds, fromLevel, toLevel, unit.TrainTimeGrowth) * count),
                speedBonus);

        /// <summary>Тривалість під бонусом швидкості — до цілих секунд, як і решта таймерів.</summary>
        public static TimeSpan Faster(TimeSpan duration, double speedBonus)
            => speedBonus <= 0
                ? duration
                : TimeSpan.FromSeconds(Math.Round(duration.TotalSeconds / (1 + speedBonus), MidpointRounding.AwayFromZero));
    }
}
