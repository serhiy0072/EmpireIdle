using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Commands
{
    /// <summary>Обнуляє прогрес квестів із Window=Daily. Викликається о 00:00 UTC.</summary>
    public record ResetDailyQuestsCommand : IRequest;

    public sealed class ResetDailyQuestsCommandHandler : IRequestHandler<ResetDailyQuestsCommand>
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

            var total = 0;

            while (true)
            {
                var stale = await _questRepository.GetStaleDailyAsync(
                    dailyKeys, now.Date, _catalog.Config.ScanBatchSize, cancellationToken);

                if (stale.Count == 0)
                    break;

                foreach (var progress in stale)
                    if (_catalog.Quests.TryGetValue(progress.QuestKey, out var config))
                        progress.Reset(config.Objectives.Select(o => o.Count), now);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                total += stale.Count;

                // Батч менший за ліміт означає, що черга вичерпана
                if (stale.Count < _catalog.Config.ScanBatchSize)
                    break;
            }

            _logger.LogInformation("Daily quests reset: {Count}", total);
        }
    }
}
