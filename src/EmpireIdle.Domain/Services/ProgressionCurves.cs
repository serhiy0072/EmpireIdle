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
    }
}
