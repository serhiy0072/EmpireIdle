using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Quests.Queries
{
    /// <summary>
    /// Id гравців, чиї дейліки треба скинути. Лише ідентифікатори:
    /// кожен гравець обробляється у власному scope, і сутність із чужого
    /// контексту там не збережеться.
    /// </summary>
    public record GetPlayerIdsWithStaleDailyQuestsQuery : IRequest<IReadOnlyList<Guid>>;

    public sealed class GetPlayerIdsWithStaleDailyQuestsQueryHandler
        : IRequestHandler<GetPlayerIdsWithStaleDailyQuestsQuery, IReadOnlyList<Guid>>
    {
        private readonly IQuestRepository _questRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetPlayerIdsWithStaleDailyQuestsQueryHandler(
            IQuestRepository questRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _questRepository = questRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<Guid>> Handle(GetPlayerIdsWithStaleDailyQuestsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var dailyKeys = _catalog.Config.Quests
                .Where(q => q.Window == QuestWindow.Daily)
                .Select(q => q.Key)
                .ToHashSet();

            return _questRepository.GetPlayerIdsWithStaleDailyAsync(
                dailyKeys, now.Date, _catalog.Config.ScanBatchSize, cancellationToken);
        }
    }
}
