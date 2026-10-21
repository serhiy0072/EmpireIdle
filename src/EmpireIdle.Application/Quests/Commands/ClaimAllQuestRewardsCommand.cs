using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>Забрати нагороди всіх виконаних квестів одним запитом.</summary>
    public record ClaimAllQuestRewardsCommand(Guid PlayerId)
        : IRequest<IReadOnlyList<string>>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник: забирає те, що список показує завершеним (<see cref="QuestBoard"/>), і
    /// зберігає все одним SaveChanges — без напівзібраного стану, якщо щось упаде посередині.
    /// Нічого забирати — не відмова: кнопка могла застаріти, поки гравець думав.
    /// </summary>
    public sealed class ClaimAllQuestRewardsCommandHandler : IRequestHandler<ClaimAllQuestRewardsCommand, IReadOnlyList<string>>
    {
        private readonly IQuestRepository _questRepository;
        private readonly QuestThresholds _thresholds;
        private readonly RewardDispatcher _rewards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ClaimAllQuestRewardsCommandHandler> _logger;

        public ClaimAllQuestRewardsCommandHandler(
            IQuestRepository questRepository,
            QuestThresholds thresholds,
            RewardDispatcher rewards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<ClaimAllQuestRewardsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _thresholds = thresholds;
            _rewards = rewards;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> Handle(ClaimAllQuestRewardsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Увесь прогрес одним запитом: SyncAsync нижче отримує той самий екземпляр,
            // тож новий рядок порогу не заведеться двічі
            var progressByKey = (await _questRepository.GetAllAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            var levels = await _thresholds.LevelsAsync(request.PlayerId, cancellationToken);

            var claimable = QuestBoard.Visible(_catalog.Quests.Values, progressByKey, levels, now)
                .Where(entry => entry.State == QuestState.Completed)
                .ToList();

            var claimed = new List<string>();

            foreach (var entry in claimable)
            {
                var config = entry.Config;

                // Поріг, досягнутий до відкриття квесту, фіксується тут — як і в поштучному клеймі
                var progress = await _thresholds.SyncAsync(request.PlayerId, config,
                    progressByKey.GetValueOrDefault(config.Key), now, cancellationToken);

                if (progress is null || !progress.Claim(now))
                    continue;

                await _rewards.GrantAllAsync(request.PlayerId, config.Rewards, $"quest:{config.Key}", now, cancellationToken);

                claimed.Add(config.Key);
            }

            if (claimed.Count == 0)
                return claimed;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} claimed {Count} quests at once", request.PlayerId, claimed.Count);

            return claimed;
        }
    }
}
