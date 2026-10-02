using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>Забрати нагороду за виконаний квест.</summary>
    public record ClaimQuestRewardCommand(Guid PlayerId, string QuestKey)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник: переводить квест у Claimed і видає нагороди.
    /// Перехід стану йде ПЕРЕД видачею — якщо квест уже забраний,
    /// нагорода не видається взагалі.
    /// </summary>
    public sealed class ClaimQuestRewardCommandHandler : IRequestHandler<ClaimQuestRewardCommand>
    {
        private readonly IQuestRepository _questRepository;
        private readonly QuestThresholds _thresholds;
        private readonly RewardDispatcher _rewards;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ClaimQuestRewardCommandHandler> _logger;

        public ClaimQuestRewardCommandHandler(
            IQuestRepository questRepository,
            QuestThresholds thresholds,
            RewardDispatcher rewards,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<ClaimQuestRewardCommandHandler> logger)
        {
            _questRepository = questRepository;
            _thresholds = thresholds;
            _rewards = rewards;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(ClaimQuestRewardCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Ключ приходить від клієнта: невідомий — це 404, а не збій каталогу
            if (!_catalog.Quests.TryGetValue(request.QuestKey, out var config))
                throw new EntityNotFoundException("Quest", request.QuestKey);

            if (config.Scope != QuestScope.Personal)
                throw new RequirementNotMetException($"Quest '{request.QuestKey}' is server-scoped — its rewards are granted on completion.");

            // Ті самі межі, що й у списку: квест, якого гравець не бачить, не забирається прямим запитом.
            // Вікно події може закритись, поки список відкритий, — тому це відмова з причиною
            if (!config.IsOpenAt(now))
                throw new InvalidStateException(RefusalReasons.QuestNotClaimable, $"Quest '{request.QuestKey}' is outside its active window.");

            if (config.Prerequisite is { } prerequisiteKey && !await IsCompletedAsync(request.PlayerId, prerequisiteKey, now, new HashSet<string>(), cancellationToken))
                throw new RequirementNotMetException($"Quest '{request.QuestKey}' is locked behind '{prerequisiteKey}'.");

            // Поріг (рівень будівлі), досягнутий до відкриття квесту, фіксується тут, а не в запиті
            // списку: список показав квест завершеним, тож і забрати його мусить бути можна
            var progress = await _thresholds.SyncAsync(request.PlayerId, config,
                    await _questRepository.GetAsync(request.PlayerId, request.QuestKey, cancellationToken),
                    now, cancellationToken)
                ?? throw new EntityNotFoundException("Quest progress", request.QuestKey);

            // Claim повертає false, якщо квест не завершений або вже забраний
            if (!progress.Claim(now))
                throw new InvalidStateException(RefusalReasons.QuestNotClaimable,
                    $"Quest '{request.QuestKey}' is not claimable (state: {progress.State}).");

            await _rewards.GrantAllAsync(request.PlayerId, config.Rewards, $"quest:{request.QuestKey}", now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} claimed quest {QuestKey}", request.PlayerId, request.QuestKey);
        }

        /// <summary>
        /// Ланцюжок відкривається завершенням, а не клеймом — як у списку й трекері. Поріг
        /// пререквізиту, досягнутий до його відкриття, теж рахується, але лише коли сама ланка
        /// вже відкрита: інакше ратуша 3 закрила б другий квест повз невиконаний перший.
        /// </summary>
        private async Task<bool> IsCompletedAsync(Guid playerId, string questKey, DateTime now,
            HashSet<string> visited, CancellationToken cancellationToken)
        {
            // Цикл у конфігу не має зациклити запит
            if (!visited.Add(questKey))
                return false;

            var stored = await _questRepository.GetAsync(playerId, questKey, cancellationToken);

            // Збережене завершення вже пройшло свій ланцюжок: зачинених квестів трекер не рухає
            if (stored is not null && stored.State != QuestState.InProgress)
                return true;

            var config = _catalog.Quest(questKey);

            if (config.Prerequisite is { } previous
                && !await IsCompletedAsync(playerId, previous, now, visited, cancellationToken))
                return false;

            var progress = await _thresholds.SyncAsync(playerId, config, stored, now, cancellationToken);

            return progress is not null && progress.State != QuestState.InProgress;
        }
    }
}
