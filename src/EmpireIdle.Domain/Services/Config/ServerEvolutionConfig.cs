namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Ріст рівня світу (GDD §2.7, рішення 04.10.2026): рівень не має стелі й росте з часом —
    /// раз на <see cref="DaysPerLevel"/> днів, без умов зрілості. Щільність лише закриває реєстрацію.
    /// </summary>
    public class ServerEvolutionConfig
    {
        /// <summary>Частка площі туману, зайнята селами, після якої реєстрація закривається.</summary>
        public double DensityThreshold { get; set; } = 0.35;

        /// <summary>Скільки днів триває один рівень світу. Заглушка до Режисера.</summary>
        public int DaysPerLevel { get; set; } = 45;
    }
}
