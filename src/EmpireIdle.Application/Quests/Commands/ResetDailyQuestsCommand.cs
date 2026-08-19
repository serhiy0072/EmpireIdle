using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>Обнуляє прогрес квестів із Window=Daily. Викликається о 00:00 UTC.</summary>
    public record ResetDailyQuestsCommand : IRequest;

    public class ResetDailyQuestsCommandHandler : IRequestHandler<ResetDailyQuestsCommand>
    {
        private readonly IQuestRepository _questRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<ResetDailyQuestsCommandHandler> _logger;

        public ResetDailyQuestsCommandHandler(
            IQuestRepository questRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<ResetDailyQuestsCommandHandler> logger)
        {
            _questRepository = questRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(ResetDailyQuestsCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var dailyKeys = _catalog.Quests.Values
                .Where(q => q.Window == QuestWindow.Daily)
                .Select(q => q.Key)
                .ToHashSet();

            if (dailyKeys.Count == 0)
                return;

            var stale = await _questRepository.GetByKeysAsync(dailyKeys, now.Date, cancellationToken);

            foreach (var progress in stale)
                progress.Reset(_catalog.Quest(progress.QuestKey).Objectives.Select(o => o.Count), now);

            if (stale.Count > 0)
                await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Daily quests reset: {Count}", stale.Count);
        }
    }
}
