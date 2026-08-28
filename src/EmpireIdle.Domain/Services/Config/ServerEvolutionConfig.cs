namespace EmpireIdle.Domain.Services
{
    /// <summary>
    /// Коли світ переходить на наступний рівень.
    ///
    /// Інтервал — нижня межа, не розклад: ап стається, коли минув строк
    /// І світ дозрів. Повільний світ чекатиме довше сам собою.
    /// </summary>
    public class ServerEvolutionConfig
    {
        /// <summary>Частка площі туману, зайнята селами, після якої реєстрація закривається.</summary>
        public double DensityThreshold { get; set; } = 0.35;

        /// <summary>
        /// Наскільки медіана ратуші може відставати від стелі, щоб світ вважався зрілим.
        /// 2 означає: світ 1 рівня росте, коли медіана дійшла 8 із 10.
        /// </summary>
        public int MaturityMarginLevels { get; set; } = 2;

        /// <summary>Мінімум днів між підйомами рівня.</summary>
        public int MinDaysBetweenLevels { get; set; } = 45;
    }
}
