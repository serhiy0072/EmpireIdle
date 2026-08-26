using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>
    /// Скидає дейліки одного гравця. Одиниця роботи джоба:
    /// конфлікт паралелізму коштує цього гравця, а не всю чергу.
    /// </summary>
    public record ResetPlayerDailyQuestsCommand(Guid PlayerId) : IRequest;

    public sealed class ResetPlayerDailyQuestsCommandHandler : IRequestHandler<ResetPlayerDailyQuestsCommand>
    {
        private readonly IQuestRepository _questRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ResetPlayerDailyQuestsCommandHandler> _logger;

        public ResetPlayerDailyQuestsCommandHandler(
            IQuestRepository questRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<ResetPlayerDailyQuestsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(ResetPlayerDailyQuestsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var dailyKeys = _catalog.Config.Quests
                .Where(q => q.Window == QuestWindow.Daily)
                .Select(q => q.Key)
                .ToHashSet();

            var stale = await _questRepository.GetStaleDailyForPlayerAsync(
                request.PlayerId, dailyKeys, now.Date, cancellationToken);

            if (stale.Count == 0)
                return;

            foreach (var progress in stale)
            {
                var quest = _catalog.Quest(progress.QuestKey);
                progress.Reset(quest.Objectives.Select(o => o.Count), now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Reset {Count} daily quests for player {PlayerId}", stale.Count, request.PlayerId);
        }
    }
}
