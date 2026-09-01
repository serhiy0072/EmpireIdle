using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.ServerQuests.Queries
{
    /// <summary>Серверний квест очима конкретного гравця.</summary>
    public record ServerQuestView(
        string Key,
        string DisplayName,
        long Total,
        long Target,
        QuestState State,
        DateTime? CompletedAt,
        long MyContribution,
        int MyRank);

    public record GetServerQuestsQuery(Guid PlayerId) : IRequest<List<ServerQuestView>>, IPlayerScopedRequest;

    public sealed class GetServerQuestsQueryHandler : IRequestHandler<GetServerQuestsQuery, List<ServerQuestView>>
    {
        private readonly IServerQuestRepository _questRepository;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public GetServerQuestsQueryHandler(
            IServerQuestRepository questRepository,
            GameCatalog catalog,
            TimeProvider timeProvider)
        {
            _questRepository = questRepository;
            _catalog = catalog;
            _timeProvider = timeProvider;
        }

        public async Task<List<ServerQuestView>> Handle(GetServerQuestsQuery request,
            CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var configs = _catalog.Config.Quests
                .Where(q => q.Scope == QuestScope.Server)
                .Where(q => (q.ActiveFrom is not { } from || now >= from)
                            && (q.ActiveTo is not { } to || now <= to))
                .ToList();

            var views = new List<ServerQuestView>(configs.Count);

            foreach (var config in configs)
            {
                var progress = await _questRepository.GetProgressAsync(config.Key, cancellationToken);

                // Рядок створює джоб підрахунку — до його першого прогону
                // квест показується з нульовим підсумком, а не ховається
                var target = progress?.Target ?? config.Objectives.Sum(o => (long)o.Count);

                var ranked = await _questRepository.GetRankedAsync(config.Key, cancellationToken);
                var index = ranked.FindIndex(c => c.PlayerId == request.PlayerId);

                views.Add(new ServerQuestView(
                    config.Key,
                    config.DisplayName,
                    progress?.Total ?? 0,
                    target,
                    progress?.State ?? QuestState.InProgress,
                    progress?.CompletedAt,
                    index >= 0 ? ranked[index].Amount : 0,
                    index >= 0 ? index + 1 : 0));
            }

            return views;
        }
    }
}
