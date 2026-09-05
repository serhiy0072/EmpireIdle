using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>Кладе предмет в інвентар: доповнює наявний стек або створює новий.</summary>
    public class ItemRewardGranter : IRewardGranter
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly GameCatalog _catalog;

        public ItemRewardGranter(IInventoryRepository inventoryRepository, GameCatalog catalog)
        {
            _inventoryRepository = inventoryRepository;
            _catalog = catalog;
        }

        /// <inheritdoc/>
        public string RewardType => "Item";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Item reward from '{context.Reference}' has no Key.");

            // Кидає, якщо предмета немає в каталозі — краще впасти при видачі,
            // ніж покласти в інвентар предмет, який неможливо використати
            _catalog.Item(key);

            var stack = await _inventoryRepository.GetItemAsync(context.PlayerId, key, cancellationToken);

            if (stack is null)
            {
                await _inventoryRepository.AddItemAsync(
                    new PlayerItem(Guid.NewGuid(), context.PlayerId, key, context.Reward.Amount), cancellationToken);
                return;
            }

            stack.Add(context.Reward.Amount);
        }
    }
}
