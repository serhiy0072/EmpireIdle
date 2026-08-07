namespace EmpireIdle.Domain.Services
{
    /// <summary>Параметри монетизації.</summary>
    public class MonetizationConfig
    {
        /// <summary>Скільки gems коштує прискорення на одну хвилину.</summary>
        public int SpeedUpGemsPerMinute { get; set; } = 2;

        /// <summary>Мінімальна ціна прискорення.</summary>
        public int SpeedUpMinGems { get; set; } = 1;

        /// <summary>Скільки хвилин до кінця можна завершити безкоштовно (зручність UX).</summary>
        public int InstantFinishThresholdMinutes { get; set; } = 5;
    }
}