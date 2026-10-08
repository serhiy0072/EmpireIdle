using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Параметри монетизації.</summary>
    public class MonetizationConfig
    {
        /// <summary>
        /// Скільки секунд кожного таймера прискорення не зрізає (рішення 08.10.2026):
        /// будівництво й тренування — 0, марш — 30. Таймера немає в списку — межа нуль.
        /// </summary>
        public Dictionary<SpeedUpTimer, int> SpeedUpFloorSeconds { get; set; } = new();

        /// <summary>Скільки gems коштує вилікувати одного пораненого.</summary>
        public int HealGemsPerUnit { get; set; } = 1;
        /// <summary>Множник у формулі прискорення: ceil(Factor × хвилин^Exponent).</summary>
        public double SpeedUpFactor { get; set; } = 1.2;

        /// <summary>Показник степеня. &lt;1 робить довгі таймери відносно дешевшими.</summary>
        public double SpeedUpExponent { get; set; } = 0.75;
    }
}
