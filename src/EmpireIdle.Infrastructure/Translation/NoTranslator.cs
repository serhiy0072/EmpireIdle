using EmpireIdle.Application.Interfaces;

namespace EmpireIdle.Infrastructure.Translation
{
    /// <summary>
    /// Перекладача немає: провайдера не обрано (GDD §10). Чат працює й без
    /// нього — гравець бачить оригінал. Підключення провайдера — це нова
    /// реалізація ITranslator і рядок реєстрації, без змін у чаті.
    /// </summary>
    public sealed class NoTranslator : ITranslator
    {
        public bool IsAvailable => false;

        public Task<string?> TranslateAsync(string text, string fromLanguage, string toLanguage,
            CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
}
