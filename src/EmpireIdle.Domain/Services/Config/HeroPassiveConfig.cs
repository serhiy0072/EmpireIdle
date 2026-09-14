namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Пасивне вміння героя: постійний бонус війську, яким він командує.
    ///
    /// Ціль і стат — рядки, не enum, з тієї самої причини, що й клас героя
    /// (§5.1): нова пасивка має бути рядком у JSON, а не міграцією.
    /// </summary>
    public class HeroPassiveConfig
    {
        /// <summary>Ключ, унікальний у межах героя.</summary>
        public string Key { get; set; } = null!;

        /// <summary>Назва для екрана героя.</summary>
        public string DisplayName { get; set; } = null!;

        /// <summary>
        /// Із якого сузір'я вміння працює. Нуль означає «з першого дня»:
        /// у героя завжди є хоча б одна пасивка, інакше він порожній.
        /// </summary>
        public int UnlockConstellation { get; set; }

        /// <summary>
        /// Ключ типу юніта або "all" на все військо. Клас героя на це
        /// не впливає: лучник може підсилювати піхоту, якщо так задумано.
        /// </summary>
        public string Target { get; set; } = "all";

        /// <summary>Який стат підсилює: "Attack" або "Defense".</summary>
        public string Stat { get; set; } = null!;

        /// <summary>Бонус у відсотках на момент відкриття.</summary>
        public double BasePercent { get; set; }

        /// <summary>Приріст за кожне сузір'я понад те, що відкрило вміння.</summary>
        public double PercentPerConstellation { get; set; }
    }
}
