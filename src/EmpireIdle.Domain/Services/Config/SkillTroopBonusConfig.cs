namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Бонус вміння війську, яким командує герой.
    ///
    /// Ціль і стат — рядки, не enum (§5.1): нова ціль має бути рядком у JSON, а не міграцією.
    /// </summary>
    public class SkillTroopBonusConfig
    {
        /// <summary>
        /// Ключ типу юніта або "all" на все військо. Клас героя на це
        /// не впливає: лучник може підсилювати піхоту, якщо так задумано.
        /// </summary>
        public string Target { get; set; } = "all";

        /// <summary>Який стат підсилює: "Attack" або "Defense".</summary>
        public string Stat { get; set; } = null!;

        /// <summary>Бонус у відсотках на кожному рівні вміння, від першого: [20, 40, 60, …].</summary>
        public List<double> Percents { get; set; } = new();
    }
}
