using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Нараховує ресурси в село. Обрізається по капу складу —
    /// інакше нагорода вивела б гравця за межі, які тримає домен.
    /// </summary>
    public class ResourceRewardGranter : IRewardGranter
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ResourceRewardGranter> _logger;

        public ResourceRewardGranter(IVillageRepository villageRepository, GameCatalog catalog,
            ILogger<ResourceRewardGranter> logger)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _logger = logger;
        }

        /// <inheritdoc/>
        public string RewardType => "Resource";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Resource reward from '{context.Reference}' has no Key.");

            if (!_catalog.Resources.ContainsKey(key))
                throw new InvalidOperationException($"Reward references unknown resource '{key}'.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {context.PlayerId}.");

            var granted = village.GrantResource(key, context.Reward.Amount);

            if (granted < context.Reward.Amount)
                _logger.LogInformation(
                    "Reward from {Reference} capped for player {PlayerId}: {Granted} of {Requested} {Resource}",
                    context.Reference, context.PlayerId, granted, context.Reward.Amount, key);
        }
    }
}
