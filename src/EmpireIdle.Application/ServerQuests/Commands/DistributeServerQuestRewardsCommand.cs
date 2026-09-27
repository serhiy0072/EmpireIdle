using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.ServerQuests.Commands
{
    /// <summary>
    /// Роздає нагороди за завершений серверний квест.
    ///
    /// Ранг визначається порядком внесків: більший раніше, нічия — за часом
    /// останнього внеску. Без другого критерію ранги були б недетерміновані,
    /// і два прогони джоба дали б різні нагороди.
    /// </summary>
    public record DistributeServerQuestRewardsCommand(string QuestKey) : IRequest;

    public sealed class DistributeServerQuestRewardsCommandHandler
        : IRequestHandler<DistributeServerQuestRewardsCommand>
    {
        /// <summary>Скільки внесків на одне збереження.</summary>
        private const int BatchSize = 200;

        private readonly IServerQuestRepository _questRepository;
        private readonly RewardDispatcher _rewards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DistributeServerQuestRewardsCommandHandler> _logger;

        public DistributeServerQuestRewardsCommandHandler(
            IServerQuestRepository questRepository,
            RewardDispatcher rewards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<DistributeServerQuestRewardsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _rewards = rewards;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DistributeServerQuestRewardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var config = _catalog.Quests.GetValueOrDefault(request.QuestKey);

            if (config is null || config.RewardTiers.Count == 0)
                return;

            var progress = await _questRepository.GetProgressAsync(request.QuestKey, cancellationToken);

            if (progress is null || progress.State != QuestState.Completed)
                return;

            // Внески вже відфільтровані по Amount > 0: ярус «всі інші»
            // не має діставатись тим, хто не грав
            var ranked = await _questRepository.GetRankedAsync(request.QuestKey, cancellationToken);

            var granted = 0;

            // Пачками, кожна своїм збереженням: нагорода пише в гаманці й склади, а гравці
            // паралельно грають, тож конфлікт xmin неминучий. Невдала пачка зупиняє прохід —
            // після скидання трекера решта сутностей відірвана, — а наступний прохід джоба
            // підхопить ще не позначені внески: ранги ті самі, суми після завершення не змінюються
            for (var start = 0; start < ranked.Count; start += BatchSize)
            {
                var inBatch = 0;

                for (var index = start; index < Math.Min(start + BatchSize, ranked.Count); index++)
                {
                    var contribution = ranked[index];
                    var rank = index + 1;

                    // Позначаємо ДО видачі: повторний прогін джоба після збою
                    // всередині циклу не має видати нагороду вдруге
                    if (!contribution.MarkRewarded(rank, now))
                        continue;

                    var tier = FindTier(config, rank);

                    if (tier is null)
                        continue;

                    await _rewards.GrantAllAsync(
                        contribution.PlayerId, tier.Rewards, request.QuestKey, now, cancellationToken);

                    inBatch++;
                }

                if (!await _unitOfWork.TrySaveChangesAsync(cancellationToken))
                {
                    _logger.LogWarning("Server quest {QuestKey}: batch from rank {Rank} hit a concurrency conflict; next run resumes",
                        request.QuestKey, start + 1);
                    break;
                }

                granted += inBatch;
            }

            _logger.LogInformation("Server quest {QuestKey} rewarded {Count} contributors",
                request.QuestKey, granted);
        }

        /// <summary>
        /// Перший ярус, чий поріг ≥ рангу. MaxRank = null означає «всі інші»
        /// й має стояти останнім — інакше він перехопить усіх.
        /// </summary>
        private static RewardTierConfig? FindTier(QuestConfig config, int rank)
            => config.RewardTiers
                .OrderBy(t => t.MaxRank ?? int.MaxValue)
                .FirstOrDefault(t => t.MaxRank is null || rank <= t.MaxRank);
    }
}
