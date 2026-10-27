namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Криві прогресії будівель. Зібрані в одному місці, бо вони пов'язані:
    /// співвідношення між ними визначає, що саме є вузьким горлом на кожному
    /// етапі гри — ресурси на ранньому, час на пізньому.
    /// </summary>
    public static class ProgressionCurves
    {
        /// <summary>
        /// Вартість апгрейду з поточного рівня на наступний.
        /// Геометрична: інакше на високих рівнях ресурси перестають бути
        /// обмеженням і лишається тільки час, тобто тільки прискорення за gems.
        /// </summary>
        public static int UpgradeCost(int baseCost, int currentLevel, double growth)
        {
            var cost = baseCost * Math.Pow(growth, currentLevel - 1);

            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        /// <summary>
        /// Місткість буфера будівлі. Лінійна, бо виробіток лінійний:
        /// буфер лишається сталим у годинах на всіх рівнях, і гравець заходить
        /// двічі на добу незалежно від того, який у нього рівень.
        /// </summary>
        public static int BufferCap(int baseStorage, int level)
        {
            var cap = (long)baseStorage * level;

            return cap >= int.MaxValue ? int.MaxValue : (int)cap;
        }

        /// <summary>
        /// Час апгрейду з рівня. Поверх геометричного росту накладає спадний
        /// "ранній податок": на рівні 1 час зростає найсильніше (×3), до рівня
        /// ~15 ефект згасає до кількох відсотків. Перші рівні мали відчуватись
        /// миттєвими — це їх сповільнює, не займаючи пізню гру.
        /// </summary>
        public static double BuildMinutes(int baseMinutes, double growth, int level)
        {
            const double earlyTaxStrength = 2.0;
            const double earlyTaxDecay = 1.2;

            var geometric = baseMinutes * Math.Pow(growth, level - 1);
            var earlyMultiplier = 1 + earlyTaxStrength / Math.Pow(level, earlyTaxDecay);

            return geometric * earlyMultiplier;
        }

        /// <summary>
        /// Множник до бойових статів юніта від рівня. Лінійний: +<paramref name="bonusPerLevel"/> за рівень
        /// (GameConfig.UnitLevelStatBonus), рівень 1 — база (×1.0).
        /// </summary>
        public static double UnitStatMultiplier(int level, double bonusPerLevel) => 1 + bonusPerLevel * (level - 1);

        /// <summary>
        /// Скільки юнітів свого типу вміщує будівля юнітів: від min на першому рівні до max на maxLevel,
        /// геометрично — кожен рівень множить місткість на однаковий коефіцієнт (рішення 13.10.2026).
        /// </summary>
        public static int UnitCapacity(int buildingLevel, int maxLevel, int min, int max)
        {
            if (maxLevel <= 1)
                return max;

            var level = Math.Clamp(buildingLevel, 1, maxLevel);
            var growth = Math.Pow(max / (double)min, 1.0 / (maxLevel - 1));

            return (int)Math.Round(min * Math.Pow(growth, level - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Скільки юнітів будівля тренує чи качає за одну партію: від min на першому рівні
        /// до max на maxLevel, лінійно, з округленням до цілого (рішення 12.10.2026).
        /// </summary>
        public static int UnitBatchSize(int buildingLevel, int maxLevel, int min, int max)
        {
            if (maxLevel <= 1)
                return max;

            var level = Math.Clamp(buildingLevel, 1, maxLevel);

            return (int)Math.Round(min + (max - min) * (level - 1) / (double)(maxLevel - 1), MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Сумарна вартість (чи час) прокачки з fromLevel у toLevel: крок за
        /// кроком, геометрична крива на кожен крок. Стрибок через кілька
        /// рівнів коштує суму кроків, а не різницю кінцевих цін (§5.2 GDD) —
        /// інакше якнайшвидший стрибок на топ-рівень був би вигіднішим за
        /// поступову прокачку.
        /// </summary>
        public static int CumulativeUnitLevelCost(int baseAmount, int fromLevel, int toLevel, double growth)
        {
            var total = 0.0;

            for (var level = fromLevel; level < toLevel; level++)
                total += baseAmount * Math.Pow(growth, level - 1);

            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
