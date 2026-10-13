using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.ServerQuests.Commands
{
    /// <summary>
    /// Роздає нагороду за завершений серверний квест (GDD §2.7, §8.4): кожному гравцю світу —
    /// лист із нагородою для всіх, а топу за внеском — ще й бонус свого ярусу в тому самому листі.
    ///
    /// Ранг визначається порядком внесків: більший раніше, нічия — за часом
    /// останнього внеску. Без другого критерію ранги були б недетерміновані,
    /// і два прогони джоба дали б різні бонуси.
    /// </summary>
    public record DistributeServerQuestRewardsCommand(string QuestKey) : IRequest;

    public sealed class DistributeServerQuestRewardsCommandHandler
        : IRequestHandler<DistributeServerQuestRewardsCommand>
    {
        /// <summary>Скільки листів на одне збереження.</summary>
        private const int BatchSize = 200;

        private readonly IServerQuestRepository _questRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IMailRepository _mailRepository;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<DistributeServerQuestRewardsCommandHandler> _logger;

        public DistributeServerQuestRewardsCommandHandler(
            IServerQuestRepository questRepository,
            IPlayerRepository playerRepository,
            IMailRepository mailRepository,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<DistributeServerQuestRewardsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _playerRepository = playerRepository;
            _mailRepository = mailRepository;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(DistributeServerQuestRewardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var config = _catalog.Quests.GetValueOrDefault(request.QuestKey);

            if (config is null)
                return;

            var progress = await _questRepository.GetProgressAsync(request.QuestKey, cancellationToken);

            if (progress is null || progress.State != QuestState.Completed || progress.RewardsMailedAt is not null)
                return;

            // Ранги рахуємо один раз на прохід: суми після завершення вже не змінюються.
            // Внески відфільтровані по Amount > 0 — бонус топу не дістається тим, хто не грав
            var ranked = (await _questRepository.GetRankedAsync(request.QuestKey, cancellationToken))
                .Select((contribution, index) => (Contribution: contribution, Rank: index + 1))
                .ToDictionary(entry => entry.Contribution.PlayerId);

            var expiresAt = now.AddDays(_catalog.Config.Mail.RewardRetentionDays);
            var mailed = 0;

            // Пачками, кожна своїм збереженням разом із курсором: лист і позначка «вже відправлено»
            // фіксуються атомарно. Конфлікт зупиняє прохід — трекер скинуто, а наступний прогін
            // джоба продовжить від збереженого курсора
            while (true)
            {
                var players = await _playerRepository.GetIdsAfterAsync(progress.MailedThroughPlayerId, BatchSize,
                    cancellationToken);

                if (players.Count == 0)
                {
                    progress.FinishMailing(now);

                    if (!await _unitOfWork.TrySaveChangesAsync(cancellationToken))
                        _logger.LogWarning("Server quest {QuestKey}: finishing the mailing hit a conflict; next run retries",
                            request.QuestKey);

                    break;
                }

                foreach (var playerId in players)
                {
                    var rewards = RewardsFor(config, playerId, ranked, now);

                    if (rewards.Count > 0)
                        await _mailRepository.AddLetterAsync(MailLetter.WithRewards(Guid.NewGuid(), _serverContext.ServerId,
                            playerId, MailKind.ServerQuestReward, rewards, null, now, expiresAt), cancellationToken);
                }

                progress.AdvanceMailing(players[^1]);

                if (!await _unitOfWork.TrySaveChangesAsync(cancellationToken))
                {
                    _logger.LogWarning("Server quest {QuestKey}: batch after {PlayerId} hit a concurrency conflict; next run resumes",
                        request.QuestKey, players[0]);
                    break;
                }

                mailed += players.Count;
            }

            _logger.LogInformation("Server quest {QuestKey} mailed rewards to {Count} players", request.QuestKey, mailed);
        }

        /// <summary>
        /// Нагорода для всіх плюс бонус ярусу, якщо гравець вніс. Позначка на внеску ставиться тут же:
        /// вона йде в одну транзакцію з листом і шле гравцю realtime-сповіщення про ранг.
        /// </summary>
        private static List<MailReward> RewardsFor(QuestConfig config, Guid playerId,
            Dictionary<Guid, (ServerQuestContribution Contribution, int Rank)> ranked, DateTime now)
        {
            var rewards = config.Rewards.Select(ToMail).ToList();

            if (ranked.TryGetValue(playerId, out var entry) && entry.Contribution.MarkRewarded(entry.Rank, now)
                && FindTier(config, entry.Rank) is { } tier)
                rewards.AddRange(tier.Rewards.Select(ToMail));

            // Той самий ресурс із двох джерел — одним рядком: гравець бачить суму, а не два однакові пункти
            return rewards
                .GroupBy(r => (r.Type, r.Key))
                .Select(group => new MailReward(group.Key.Type, group.Key.Key, group.Sum(r => r.Amount)))
                .ToList();
        }

        private static MailReward ToMail(RewardConfig reward) => new(reward.Type, reward.Key, reward.Amount);

        /// <summary>
        /// Перший ярус, чий поріг ≥ рангу. MaxRank = null означає «всі інші, хто вніс»
        /// й має стояти останнім — інакше він перехопить усіх.
        /// </summary>
        private static RewardTierConfig? FindTier(QuestConfig config, int rank)
            => config.RewardTiers
                .OrderBy(t => t.MaxRank ?? int.MaxValue)
                .FirstOrDefault(t => t.MaxRank is null || rank <= t.MaxRank);
    }
}
