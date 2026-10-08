namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Склад вмінь героя однієї рідкості (GDD §6.1, рішення 08.10.2026): скільки вмінь у кожній
    /// половині й скільки з них небойових. Активне завжди одне й завжди в атаці.
    /// </summary>
    public class SkillLayoutConfig
    {
        /// <summary>Скільки вмінь в атакувальній половині, включно з активним.</summary>
        public int Attack { get; set; }

        /// <summary>Скільки вмінь у захисній половині, включно з небойовими.</summary>
        public int Defense { get; set; }

        /// <summary>Скільки з захисних — небойові.</summary>
        public int Utility { get; set; }
    }
}
