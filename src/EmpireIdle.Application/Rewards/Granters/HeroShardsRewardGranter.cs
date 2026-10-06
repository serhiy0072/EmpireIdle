using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Осколки героя як нагорода — з банерів і скриньок (GDD §6.1). Надлишок прокачаного героя
    /// банк осколків сам перетворює на універсальні.
    /// </summary>
    public class HeroShardsRewardGranter : IRewardGranter
    {
        private readonly HeroShardBank _shards;

        public HeroShardsRewardGranter(HeroShardBank shards) => _shards = shards;

        /// <inheritdoc/>
        public string RewardType => "HeroShards";

        /// <inheritdoc/>
        public Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"HeroShards reward from '{context.Reference}' has no Key.");

            return _shards.AddAsync(context.PlayerId, key, context.Reward.Amount, cancellationToken);
        }
    }
}
