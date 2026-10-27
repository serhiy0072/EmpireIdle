namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Метадані поточного HTTP-запиту, потрібні Application-шару.</summary>
    public interface IRequestContext
    {
        /// <summary>Ключ ідемпотентності із заголовка Idempotency-Key; null — не переданий.</summary>
        string? IdempotencyKey { get; }

        /// <summary>
        /// Мови із заголовка Accept-Language, від бажаної: лише основний код (en-US → en).
        /// Порожній — заголовка немає або це не HTTP-запит (фонова джоба).
        /// </summary>
        IReadOnlyList<string> AcceptedLanguages { get; }
    }
}
