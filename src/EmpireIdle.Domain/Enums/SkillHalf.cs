namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Половина вмінь героя (GDD §6.1, рішення 08.10.2026). Визначає, яка книга
    /// піднімає вміння: книга атаки — лише атакувальні, книга захисту — лише захисні.
    /// </summary>
    public enum SkillHalf
    {
        /// <summary>Активне вміння й атакувальні пасивки.</summary>
        Attack = 0,

        /// <summary>Захисні пасивки; у звичайного героя ще й небойова.</summary>
        Defense = 1
    }
}
