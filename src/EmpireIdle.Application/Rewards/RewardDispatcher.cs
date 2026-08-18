using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Rewards
{
    /// <summary>Добирає й виконує видачу за типом нагороди.</summary>
    public class RewardDispatcher
    {
        private readonly Dictionary<string, IRewardGranter> _granters;

        public RewardDispatcher(IEnumerable<IRewardGranter> granters)
        {
            // DI віддає всі зареєстровані реалізації — індексуємо їх за типом
            _granters = granters.ToDictionary(g => g.RewardType, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Видає весь набір нагород. Будь-яка невідома — виняток до збереження.</summary>
        public async Task GrantAllAsync(Guid playerId, IEnumerable<RewardConfig> rewards, string reference,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            foreach (var reward in rewards)
            {
                if (!_granters.TryGetValue(reward.Type, out var granter))
                    throw new InvalidOperationException($"Reward type '{reward.Type}' is not supported.");

                await granter.GrantAsync(new RewardContext(playerId, reward, reference, utcNow), cancellationToken);
            }
        }
    }
}
