using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>Нараховує ресурси в село. Стелі складу немає (GDD §4.1).</summary>
    public class ResourceRewardGranter : IRewardGranter
    {
        private readonly IVillageRepository _villageRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public ResourceRewardGranter(IVillageRepository villageRepository, GameCatalog catalog, TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public string RewardType => "Resource";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Resource reward from '{context.Reference}' has no Key.");

            if (!_catalog.Resources.ContainsKey(key))
                throw new InvalidOperationException($"Reward references unknown resource '{key}'.");

            var village = await _villageRepository.GetByPlayerIdAsync(context.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {context.PlayerId}.");

            village.GrantResource(key, context.Reward.Amount, now);
        }
    }
}
