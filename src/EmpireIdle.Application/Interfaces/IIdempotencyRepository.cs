using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій записів ідемпотентності.</summary>
    public interface IIdempotencyRepository
    {
        Task<IdempotencyRecord?> FindAsync(Guid playerId, string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Резервує ключ окремою транзакцією. Повертає false, якщо ключ уже зайнято
        /// (унікальний індекс) — тоді операція вже виконується або виконана.
        /// </summary>
        Task<bool> TryReserveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);

        /// <summary>Знімає резерв, якщо операція впала — щоб ретрай був можливий.</summary>
        Task ReleaseAsync(Guid recordId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Дописує відповідь до вже зарезервованого запису.
        /// </summary>
        /// <param name="recordId">Id резерву, створеного <see cref="TryReserveAsync"/>.</param>
        Task CompleteAsync(Guid recordId, string? responseJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Прибирає резерви, що зависли без відповіді (процес упав між резервом і
        /// завершенням). Без цього гравець назавжди втрачає можливість повторити ключ.
        /// </summary>
        /// <returns>Кількість видалених записів.</returns>
        Task<int> PurgeStaleReservationsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
    }
}
