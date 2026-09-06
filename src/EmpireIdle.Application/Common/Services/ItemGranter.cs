using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Видає предмети гравцю: стакові додає до наявного стеку,
    /// створюючи його за потреби.
    /// </summary>
    public class ItemGranter
    {
        private readonly IInventoryRepository _repository;

        public ItemGranter(IInventoryRepository repository)
        {
            _repository = repository;
        }

        /// <summary>Видає стакові предмети.</summary>
        public async Task GrantAsync(Guid playerId, string itemKey, int count, CancellationToken cancellationToken = default)
        {
            if (count < 1)
                return;

            var existing = await _repository.GetItemAsync(playerId, itemKey, cancellationToken);

            if (existing is null)
            {
                await _repository.AddItemAsync(
                    new PlayerItem(Guid.NewGuid(), playerId, itemKey, count),
                    cancellationToken);
                return;
            }

            existing.Add(count);
        }

        /// <summary>Видає унікальний екземпляр спорядження.</summary>
        public async Task GrantEquipmentAsync(
            Guid playerId, string itemKey, EquipmentSlot slot, string rarity,
            IEnumerable<(string Stat, double Value)> stats,
            DateTime utcNow, CancellationToken cancellationToken = default)
        {
            await _repository.AddEquipmentAsync(
                new EquipmentItem(Guid.NewGuid(), playerId, itemKey, slot, rarity, stats, utcNow),
                cancellationToken);
        }
    }
}
