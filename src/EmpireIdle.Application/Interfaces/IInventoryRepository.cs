using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Репозиторій інвентаря гравця.</summary>
    public interface IInventoryRepository
    {
        /// <summary>Усі стакові предмети гравця.</summary>
        Task<List<PlayerItem>> GetItemsAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Стек конкретного типу; null — такого предмета немає.</summary>
        Task<PlayerItem?> GetItemAsync(Guid playerId, string itemKey, CancellationToken cancellationToken = default);

        /// <summary>Усе спорядження гравця.</summary>
        Task<List<EquipmentItem>> GetEquipmentAsync(Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>Конкретний екземпляр спорядження.</summary>
        Task<EquipmentItem?> GetEquipmentByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Додати стек предметів.</summary>
        Task AddItemAsync(PlayerItem item, CancellationToken cancellationToken = default);

        /// <summary>Додати екземпляр спорядження.</summary>
        Task AddEquipmentAsync(EquipmentItem equipment, CancellationToken cancellationToken = default);

        /// <summary>Прибрати порожній стек.</summary>
        void RemoveItem(PlayerItem item);
    }
}