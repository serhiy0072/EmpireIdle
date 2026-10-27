namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>
    /// Мови інтерфейсу (GDD §7.3). Коди — ISO 639-1. Російської немає й не
    /// буде: валідатор конфіга її не пропускає.
    /// </summary>
    public class LocalizationConfig
    {
        /// <summary>Мова нового гравця й мова, якою написані конфіги.</summary>
        public string DefaultLanguage { get; set; } = "uk";

        /// <summary>
        /// Мови, які гравець може обрати. За замовчуванням порожній, а не ["uk"]:
        /// прив'язка конфігу дописує значення з JSON до ініціалізованого списку.
        /// </summary>
        public List<string> Languages { get; set; } = new();

        /// <summary>Мови до вибору; порожній список означає лише мову за замовчуванням.</summary>
        public IReadOnlyList<string> SupportedLanguages => Languages.Count == 0 ? [DefaultLanguage] : Languages;

        /// <summary>
        /// Перша підтримувана з бажаних мов, інакше мова за замовчуванням:
        /// гравець бачить назви, а не помилку.
        /// </summary>
        public string Resolve(IEnumerable<string?> preferred)
            => preferred.FirstOrDefault(language => language is not null && SupportedLanguages.Contains(language)) ?? DefaultLanguage;
    }
}
