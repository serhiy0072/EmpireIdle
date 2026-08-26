using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Rewards.Granters
{
    /// <summary>Нараховує gems у гаманець акаунта.</summary>
    public class GemRewardGranter : IRewardGranter
    {
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly TimeProvider _timeProvider;

        public GemRewardGranter(IPlayerWalletRepository walletRepository, IPlayerRepository playerRepository, TimeProvider timeProvider)
        {
            _walletRepository = walletRepository;
            _playerRepository = playerRepository;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public string RewardType => "Gems";

        /// <inheritdoc/>
        public async Task GrantAsync(RewardContext context, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Гаманець належить акаунту, а не гравцю — потрібен перехід через Player
            var player = await _playerRepository.GetByIdAsync(context.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Player {context.PlayerId} not found.");

            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet not found for player {context.PlayerId}.");

            wallet.AddGems(new GemAmount(context.Reward.Amount), context.Reference, context.PlayerId, now);
        }
    }
}
