using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>
    /// Видає героя як нагороду за квест або віху. Сама логіка живе
    /// в HeroGranter — тут лише розбір конфіга нагороди.
    /// </summary>
    public class HeroRewardGranter : IRewardGranter
    {
        private readonly HeroGranter _heroGranter;

        public HeroRewardGranter(HeroGranter heroGranter)
        {
            _heroGranter = heroGranter;
        }

        /// <inheritdoc/>
        public string RewardType => "Hero";

        /// <inheritdoc/>
        public Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var key = context.Reward.Key
                ?? throw new InvalidOperationException($"Hero reward from '{context.Reference}' has no Key.");

            return _heroGranter.GrantAsync(
                context.PlayerId, key, context.Reference, context.UtcNow, cancellationToken);
        }
    }
}
