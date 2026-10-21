using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Випадковий артефакт рідкості Key — як у скринях, але простою нагородою (рішення 19.10.2026: вежа
    /// платить артефактами). Набір — з данжів, відкритих ратушею гравця (ArtifactPool); Amount — скільки
    /// окремих кидків, кожен зі своїм артефактом.
    /// </summary>
    public class ArtifactRewardGranter : IRewardGranter
    {
        private readonly ItemGranter _granter;
        private readonly IVillageRepository _villages;
        private readonly GameCatalog _catalog;
        private readonly IRandomSource _random;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ArtifactRewardGranter> _logger;

        public ArtifactRewardGranter(ItemGranter granter, IVillageRepository villages, GameCatalog catalog, IRandomSource random,
            TimeProvider timeProvider, ILogger<ArtifactRewardGranter> logger)
        {
            _granter = granter;
            _villages = villages;
            _catalog = catalog;
            _random = random;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string RewardType => "Artifact";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<Rarity>(context.Reward.Key, ignoreCase: true, out var rarity))
                throw new InvalidOperationException($"Artifact reward from '{context.Reference}' needs a rarity Key, got '{context.Reward.Key}'.");

            if (context.Reward.Amount < 1)
                throw new InvalidOperationException(
                    $"Artifact reward from '{context.Reference}' has non-positive Amount {context.Reward.Amount}.");

            var village = await _villages.GetByPlayerIdReadOnlyAsync(context.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", context.PlayerId);

            var mainLevel = village.Buildings.FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;
            var pieces = ArtifactPool.Pieces(_catalog, rarity, mainLevel);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            for (var i = 0; i < context.Reward.Amount; i++)
            {
                var key = pieces[_random.Next(pieces.Count)];
                await _granter.GrantEquipmentAsync(context.PlayerId, _catalog.Item(key), now, cancellationToken);

                _logger.LogInformation("Granted artifact {Key} ({Rarity}) to player {PlayerId} from {Reference}",
                    key, rarity, context.PlayerId, context.Reference);
            }
        }
    }
}
