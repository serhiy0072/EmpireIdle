namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Падіння міста (GDD §2.6): коли поразка виселяє село і як довго
    /// після цього діє щит. Числа — заглушки до Режисера.
    /// </summary>
    public class CityFallConfig
    {
        /// <summary>
        /// Вимкнено за замовчуванням: механіка вмикається конфігом світу,
        /// і світи без неї (та тестові конфіги) не виселяють нікого.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Виселення — лише коли Power нападника не більша за стільки Power
        /// захисника. Прибирає безкарний griefing слабших.
        /// </summary>
        public double MaxPowerRatio { get; set; } = 2.0;

        /// <summary>Скільки годин після падіння село не можна атакувати.</summary>
        public int ShieldHours { get; set; } = 24;

        /// <summary>Скільки сіл один нападник може виселити за вікно.</summary>
        public int EvictionsPerAttacker { get; set; } = 3;

        /// <summary>Вікно ліміту виселень, у годинах.</summary>
        public int EvictionWindowHours { get; set; } = 168;
    }
}
