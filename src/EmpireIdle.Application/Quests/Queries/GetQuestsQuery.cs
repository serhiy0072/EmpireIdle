using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Quests.Queries
{
    /// <summary>Квести гравця з поточним прогресом.</summary>
    public record GetQuestsQuery(Guid PlayerId) : IRequest<List<QuestView>>, IPlayerScopedRequest;

    /// <summary>Квест у поданні для клієнта.</summary>
    public record QuestView(
        string Key,
        string DisplayName,
        QuestScope Scope,
        QuestWindow Window,
        QuestState State,
        List<QuestObjectiveView> Objectives,
        List<RewardConfig> Rewards);

    /// <summary>Ціль квесту з прогресом.</summary>
    public record QuestObjectiveView(string Type, string? Target, int Amount, int Required);

    public class GetQuestsQueryHandler : IRequestHandler<GetQuestsQuery, List<QuestView>>
    {
        private readonly IQuestRepository _questRepository;
        private readonly GameCatalog _catalog;

        public GetQuestsQueryHandler(IQuestRepository questRepository, GameCatalog catalog)
        {
            _questRepository = questRepository;
            _catalog = catalog;
        }

        public async Task<List<QuestView>> Handle(GetQuestsQuery request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var progressByKey = (await _questRepository.GetAllAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            var claimed = progressByKey.Values
                .Where(p => p.State == QuestState.Claimed)
                .Select(p => p.QuestKey)
                .ToHashSet();

            var views = new List<QuestView>();

            foreach (var config in _catalog.Quests.Values.Where(c => c.Scope == QuestScope.Personal))
            {
                // Не показуємо те, чого гравець ще не відкрив або що вже поза вікном
                if (config.Prerequisite is not null && !claimed.Contains(config.Prerequisite))
                    continue;

                if (config.ActiveFrom is { } from && now < from)
                    continue;

                if (config.ActiveTo is { } to && now > to)
                    continue;

                progressByKey.TryGetValue(config.Key, out var progress);

                var objectives = config.Objectives
                    .Select((o, i) => new QuestObjectiveView(
                        o.Type,
                        o.Target,
                        progress?.Objectives.FirstOrDefault(p => p.Index == i)?.Amount ?? 0,
                        o.Count))
                    .ToList();

                views.Add(new QuestView(
                    config.Key,
                    config.DisplayName,
                    config.Scope,
                    config.Window,
                    progress?.State ?? QuestState.InProgress,
                    objectives,
                    config.Rewards));
            }

            return views;
        }
    }
}
