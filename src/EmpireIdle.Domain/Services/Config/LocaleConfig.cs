namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Переклади текстів із конфігів на одну мову. Базова мова — мова, якою
    /// написані самі конфіги (Localization.DefaultLanguage); локаль лише
    /// перекриває тексти, а чого в ній немає, показується з конфіга.
    /// </summary>
    public class LocaleConfig
    {
        /// <summary>
        /// Текст за ключем «розділ.ключ» (назва) або «розділ.ключ.поле» (опис, лор,
        /// зброя): building.townhall, hero.warrior_bran.lore, skill.warrior_bran_smash.description.
        /// Допустимі ключі — <see cref="LocaleTexts.LocalizableKeys"/>.
        /// </summary>
        public Dictionary<string, string> Texts { get; set; } = new();
    }
}
