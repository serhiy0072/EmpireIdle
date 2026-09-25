namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Параметри монетизації.</summary>
    public class MonetizationConfig
    {
        /// <summary>
        /// Скільки секунд таймера прискорення за gems не зрізає: після покупки
        /// все одно треба дочекатись цієї межі. Безкоштовного фінішу немає.
        /// </summary>
        public int SpeedUpFloorSeconds { get; set; } = 60;

        /// <summary>Скільки gems коштує вилікувати одного пораненого.</summary>
        public int HealGemsPerUnit { get; set; } = 1;
        /// <summary>Множник у формулі прискорення: ceil(Factor × хвилин^Exponent).</summary>
        public double SpeedUpFactor { get; set; } = 1.2;

        /// <summary>Показник степеня. &lt;1 робить довгі таймери відносно дешевшими.</summary>
        public double SpeedUpExponent { get; set; } = 0.75;
    }
}
