using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Видає спорядження як нагороду.
    ///
    /// Окремо від Item, бо спорядження не стакається: кожен екземпляр має
    /// власні стати, заточку й журнал роллів. Через ItemRewardGranter
    /// артефакт ліг би стаковим рядком, який ніколи не стане предметом.
    /// </summary>
    public class EquipmentRewardGranter : IRewardGranter
    {
        private readonly ItemGranter _granter;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<EquipmentRewardGranter> _logger;

        public EquipmentRewardGranter(
            ItemGranter granter,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<EquipmentRewardGranter> logger)
        {
            _granter = granter;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string RewardType => "Equipment";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Equipment reward from '{context.Reference}' has no Key.");

            var config = _catalog.Item(key);

            var slot = config.Slot
                ?? throw new InvalidOperationException($"Item '{key}' is not equipment and has no slot.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Amount — кількість окремих екземплярів, кожен зі своїм роллом.
            // Нуль означає зламаний конфіг, а не «видати один»
            if (context.Reward.Amount < 1)
                throw new InvalidOperationException(
                    $"Equipment reward from '{context.Reference}' has non-positive Amount {context.Reward.Amount}.");

            var count = context.Reward.Amount;

            for (var i = 0; i < count; i++)
                await _granter.GrantEquipmentAsync(context.PlayerId, key, slot, config.Rarity,
                    config.BaseStats.Select(s => (s.Key, s.Value)), now, cancellationToken);

            _logger.LogInformation("Granted {Count} × {Key} equipment to player {PlayerId} from {Reference}",
                count, key, context.PlayerId, context.Reference);
        }
    }
}
