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

        /// <summary>Частка ціни нового юніта, яку коштує лікування пораненого ресурсами.</summary>
        public double HealResourceCostShare { get; set; } = 0.5;

        /// <summary>Множник у формулі прискорення: ceil(Factor × хвилин^Exponent).</summary>
        public double SpeedUpFactor { get; set; } = 1.2;

        /// <summary>Показник степеня, не більше 1. &lt;1 робить довгі таймери відносно дешевшими, 1 — ціна за годину стала.</summary>
        public double SpeedUpExponent { get; set; } = 0.75;

        /// <summary>
        /// Прискорення маршу (рішення 19.10.2026): кожен варіант зрізає свою частку залишку дороги
        /// за сталу ціну — замість формули від часу.
        /// </summary>
        public List<MarchSpeedUpConfig> MarchSpeedUps { get; set; } = new();

        /// <summary>
        /// VIP-рівні (рішення 28.10.2026): скільки куплених за весь час gems відкриває кожен рівень,
        /// від першого. Порожній список — VIP вимкнено, усі на нульовому рівні.
        /// </summary>
        public List<long> VipThresholds { get; set; } = new();

        /// <summary>Кожен такий VIP-рівень додає один марш понад базові (рішення 28.10.2026: кожен парний).</summary>
        public int VipLevelsPerExtraMarch { get; set; } = 2;
    }

    /// <summary>Варіант прискорення маршу.</summary>
    public class MarchSpeedUpConfig
    {
        /// <summary>Скільки відсотків залишку дороги зрізає, 1–99.</summary>
        public int Percent { get; set; }

        /// <summary>Ціна в gems.</summary>
        public int Gems { get; set; }
    }
}
