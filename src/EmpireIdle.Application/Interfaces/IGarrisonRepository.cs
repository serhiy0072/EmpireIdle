using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій гарнізонів.</summary>
    public interface IGarrisonRepository
    {
        /// <summary>Знайти гарнізон за ідентифікатором села.</summary>
        Task<Garrison?> GetByVillageIdAsync(Guid villageId, CancellationToken cancellationToken);

        /// <summary>Гарнізон для читання (без трекінгу) — для query-сценаріїв.</summary>
        Task<Garrison?> GetByVillageIdReadOnlyAsync(Guid villageId, CancellationToken cancellationToken = default);

        /// <summary>Додати новий гарнізон.</summary>
        Task AddAsync(Garrison garrison, CancellationToken cancellationToken = default);

        /// <summary>Знайти гарнізон за ідентифікатором.</summary>
        Task<Garrison?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Id сіл, де є завершені будівництва. Без сутностей: обробка йде в іншому scope.</summary>
        Task<IReadOnlyList<Guid>> GetIdsWithDueTrainingAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>Видаляє прострочені стеки відновлюваних. Повертає кількість видалених рядків.</summary>
        Task<int> PurgeExpiredRecoverableAsync(DateTime utcNow, CancellationToken cancellationToken);

    }
}
