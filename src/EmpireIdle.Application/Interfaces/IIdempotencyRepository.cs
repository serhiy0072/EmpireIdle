using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій записів ідемпотентності.</summary>
    public interface IIdempotencyRepository
    {
        /// <summary>Знайти запис за ключем гравця.</summary>
        Task<IdempotencyRecord?> FindAsync(Guid playerId, string key, CancellationToken cancellationToken = default);

        /// <summary>Зафіксувати виконану операцію.</summary>
        Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
    }
}