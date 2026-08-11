namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Метадані поточного HTTP-запиту, потрібні Application-шару.</summary>
    public interface IRequestContext
    {
        /// <summary>Ключ ідемпотентності із заголовка Idempotency-Key; null — не переданий.</summary>
        string? IdempotencyKey { get; }
    }
}