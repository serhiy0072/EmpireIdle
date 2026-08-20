namespace EmpireIdle.Domain.Services
{
    /// <summary>Параметри монетизації.</summary>
    public class MonetizationConfig
    {
        /// <summary>Мінімальна ціна прискорення.</summary>
        public int SpeedUpMinGems { get; set; } = 1;

        /// <summary>Скільки хвилин до кінця можна завершити безкоштовно (зручність UX).</summary>
        public int InstantFinishThresholdMinutes { get; set; } = 5;

        /// <summary>Скільки gems коштує вилікувати одного пораненого.</summary>
        public int HealGemsPerUnit { get; set; } = 1;
        /// <summary>Множник у формулі прискорення: ceil(Factor × хвилин^Exponent).</summary>
        public double SpeedUpFactor { get; set; } = 1.2;

        /// <summary>Показник степеня. &lt;1 робить довгі таймери відносно дешевшими.</summary>
        public double SpeedUpExponent { get; set; } = 0.75;
    }
}
