using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Тривалість таймерів — одна формула на запуск таймера й на все, що від неї рахує
    /// (кланова допомога скорочує частку повного часу). Копії формули вже розходились:
    /// допомога не бачила множника ранніх рівнів будівництва й суми кроків тренування,
    /// тож скорочувала менше, ніж обіцяно.
    /// </summary>
    public static class TimerDurations
    {
        /// <summary>Апгрейд будівлі з поточного рівня на наступний.</summary>
        public static TimeSpan Construction(BuildingConfig config, int currentLevel)
            => TimeSpan.FromMinutes(ProgressionCurves.BuildMinutes(config.BaseBuildMinutes, config.BuildTimeGrowth, currentLevel,
                config.EarlyTaxStrength, config.EarlyTaxDecay));

        /// <summary>Тренування партії з нуля на рівень <paramref name="level"/> — сума кроків від 1 (§5.2).</summary>
        public static TimeSpan Training(UnitConfig unit, int level, int count)
            => TimeSpan.FromSeconds(
                (long)ProgressionCurves.CumulativeUnitLevelCost(unit.BaseTrainSeconds, 1, level + 1, unit.TrainTimeGrowth) * count);

        /// <summary>Прокачка партії з <paramref name="fromLevel"/> на <paramref name="toLevel"/> — лише пройдені кроки.</summary>
        public static TimeSpan LevelUp(UnitConfig unit, int fromLevel, int toLevel, int count)
            => TimeSpan.FromSeconds(
                (long)ProgressionCurves.CumulativeUnitLevelCost(unit.BaseTrainSeconds, fromLevel, toLevel, unit.TrainTimeGrowth) * count);
    }
}
