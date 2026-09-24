namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Падіння міста (GDD §2.6): програна оборона пошкоджує будівлі, а кілька
    /// поразок поспіль без повного відновлення виселяють село. Частки втрати
    /// захисту й ціна ремонту — заглушки до Режисера.
    /// </summary>
    public class CityFallConfig
    {
        /// <summary>
        /// Вимкнено за замовчуванням: механіка вмикається конфігом світу,
        /// і світи без неї (та тестові конфіги) нічого не пошкоджують.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>Яка поразка поспіль без повного відновлення виселяє.</summary>
        public int DefeatsToEvict { get; set; } = 3;

        /// <summary>Скільки випадкових будівель, крім стін, пошкоджує перша поразка серії.</summary>
        public int FirstDefeatRandomBuildings { get; set; } = 2;

        /// <summary>Скільки випадкових будівель пошкоджує кожна наступна поразка.</summary>
        public int NextDefeatRandomBuildings { get; set; } = 2;

        /// <summary>За скільки годин пошкоджена будівля відновлюється сама.</summary>
        public int RepairHours { get; set; } = 7;

        /// <summary>Темп виробництва пошкодженої будівлі — однаковий за будь-якої глибини пошкодження.</summary>
        public double DamagedProductionMultiplier { get; set; } = 0.5;

        /// <summary>Частка бонусу оборони, яку забирає кожен рівень пошкодження укріплення.</summary>
        public double DefenceLossPerDamage { get; set; } = 0.25;

        /// <summary>
        /// Ціна миттєвого ремонту: частка вартості наступного апгрейду будівлі
        /// за кожен рівень пошкодження.
        /// </summary>
        public double RepairCostShare { get; set; } = 0.1;

        /// <summary>Скільки годин після виселення село не можна атакувати.</summary>
        public int ShieldHours { get; set; } = 168;
    }
}
