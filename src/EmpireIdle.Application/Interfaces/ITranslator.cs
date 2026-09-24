namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Машинний переклад повідомлень чату (GDD §7.3). Провайдер (DeepL,
    /// Google, LLM) не обрано: це рішення про ціну й ключ, тож за замовчуванням
    /// працює реалізація без перекладу, і гравець бачить оригінал.
    /// </summary>
    public interface ITranslator
    {
        /// <summary>Чи підключений провайдер. Без нього перекладати й кешувати нема чого.</summary>
        bool IsAvailable { get; }

        /// <summary>Переклад або null, якщо провайдер не впорався — тоді показується оригінал.</summary>
        Task<string?> TranslateAsync(string text, string fromLanguage, string toLanguage,
            CancellationToken cancellationToken = default);
    }
}
