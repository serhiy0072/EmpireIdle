using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій прогресу лутбоксів.</summary>
    public interface ILootBoxProgressRepository
    {
        /// <summary>Прогрес гравця за типом лутбокса.</summary>
        Task<LootBoxProgress?> GetAsync(Guid playerId, string boxKey, CancellationToken cancellationToken = default);

        /// <summary>Додати новий запис прогресу.</summary>
        Task AddAsync(LootBoxProgress progress, CancellationToken cancellationToken = default);
    }
}
