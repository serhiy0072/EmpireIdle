namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Небойовий бонус вміння. Ефект — рядок, щоб нові (швидкість збору з точками збору, §6.1)
    /// додавались кодом лише там, де їх застосовують, а не міграцією.
    /// </summary>
    public class SkillUtilityConfig
    {
        /// <summary>Швидкість маршу героя.</summary>
        public const string MarchSpeed = "MarchSpeed";

        /// <summary>Ефекти, які гра вже вміє застосувати; решту відхиляє валідатор.</summary>
        public static readonly IReadOnlySet<string> KnownEffects = new HashSet<string> { MarchSpeed };

        public string Effect { get; set; } = null!;

        /// <summary>Бонус у відсотках на кожному рівні вміння, від першого.</summary>
        public List<double> Percents { get; set; } = new();
    }
}
