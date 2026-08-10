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
    }
}