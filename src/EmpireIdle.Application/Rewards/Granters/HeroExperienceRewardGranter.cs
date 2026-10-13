using EmpireIdle.Application.Heroes.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards.Contracts;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>Досвід героїв — у пул гравця, звідки його витрачають на обраного героя (GDD §6.1).</summary>
    public class HeroExperienceRewardGranter : IRewardGranter
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IServerContext _serverContext;

        public HeroExperienceRewardGranter(IHeroRepository heroRepository, IServerContext serverContext)
        {
            _heroRepository = heroRepository;
            _serverContext = serverContext;
        }

        /// <inheritdoc/>
        public string RewardType => "HeroExperience";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var pool = await _heroRepository.GetOrCreateExperienceAsync(context.PlayerId, _serverContext.ServerId, cancellationToken);
            pool.Add(context.Reward.Amount);
        }
    }
}
