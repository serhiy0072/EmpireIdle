using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Common.Services
{
    /// <summary>
    /// Видає предмети гравцю: стакові додає до наявного стеку,
    /// створюючи його за потреби.
    /// </summary>
    public class ItemGranter
    {
        private readonly IInventoryRepository _repository;
        private readonly IServerContext _serverContext;

        public ItemGranter(IInventoryRepository repository, IServerContext serverContext)
        {
            _repository = repository;
            _serverContext = serverContext;
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

        /// <summary>
        /// Видає унікальний екземпляр спорядження. Новий артефакт — без бонусів: базу
        /// рахують рідкість, набір і рівень, а бонуси з'являються лише із заточкою.
        /// </summary>
        /// <exception cref="InvalidOperationException">Предмет без слота — не спорядження, битий конфіг.</exception>
        public async Task GrantEquipmentAsync(
            Guid playerId, ItemConfig config, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var slot = config.Slot
                ?? throw new InvalidOperationException($"Item '{config.Key}' is not equipment and has no slot.");

            var item = new EquipmentItem(Guid.NewGuid(), playerId, _serverContext.ServerId,
                config.Key, slot, config.Rarity, utcNow);

            await _repository.AddEquipmentAsync(item, cancellationToken);
        }
    }
}
