using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.ReadModels;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;

namespace EmpireIdle.Application.Quests.Queries
{
    /// <summary>
    /// Квести гравця з поточним прогресом. Лише читання: порогові цілі (рівень будівлі)
    /// рахуються в пам'яті, а записує їх команда, що забирає нагороду.
    /// </summary>
    public record GetQuestsQuery(Guid PlayerId) : IRequest<List<QuestView>>, IPlayerScopedRequest;

    public sealed class GetQuestsQueryHandler : IRequestHandler<GetQuestsQuery, List<QuestView>>
    {
        private readonly IQuestRepository _questRepository;
        private readonly QuestThresholds _thresholds;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;

        public GetQuestsQueryHandler(IQuestRepository questRepository, QuestThresholds thresholds,
            TimeProvider timeProvider, GameCatalog catalog)
        {
            _questRepository = questRepository;
            _thresholds = thresholds;
            _timeProvider = timeProvider;
            _catalog = catalog;
        }

        public async Task<List<QuestView>> Handle(GetQuestsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var progressByKey = (await _questRepository.GetAllAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            var levels = await _thresholds.LevelsAsync(request.PlayerId, cancellationToken);
            var personal = _catalog.Quests.Values.Where(c => c.Scope == QuestScope.Personal).ToList();

            // Стан кожного квесту з урахуванням порогів — один раз, бо від нього ж залежить і ланцюжок
            var effective = personal.ToDictionary(c => c.Key, c => Evaluate(c, progressByKey.GetValueOrDefault(c.Key), levels));

            // Ланцюжок відкривається завершенням, а не клеймом
            var unlocked = effective
                .Where(e => e.Value.State != QuestState.InProgress)
                .Select(e => e.Key)
                .ToHashSet();

            var views = new List<QuestView>();

            foreach (var config in personal)
            {
                if (config.Prerequisite is not null && !unlocked.Contains(config.Prerequisite))
                    continue;

                if (config.ActiveFrom is { } from && now < from)
                    continue;

                if (config.ActiveTo is { } to && now > to)
                    continue;

                var (state, amounts) = effective[config.Key];

                var objectives = config.Objectives
                    .Select((o, i) => new QuestObjectiveView(o.Type, o.Target, amounts[i], o.Count))
                    .ToList();

                views.Add(new QuestView(
                    config.Key,
                    config.DisplayName,
                    config.Scope,
                    config.Window,
                    state,
                    objectives,
                    config.Rewards));
            }

            return views;
        }

        /// <summary>
        /// Стан і лічильники квесту, як їх побачить гравець: збережений прогрес, підтягнутий
        /// до поточних порогів. Нічого не пише — квест, закритий порогом, фіксує команда клейму.
        /// </summary>
        private static (QuestState State, int[] Amounts) Evaluate(QuestConfig config, QuestProgress? progress,
            IReadOnlyDictionary<string, int> levels)
        {
            var amounts = config.Objectives
                .Select((objective, i) =>
                {
                    var stored = progress?.Objectives.FirstOrDefault(p => p.Index == i)?.Amount ?? 0;

                    return QuestThresholds.CurrentValue(objective, levels) is int level ? Math.Max(stored, level) : stored;
                })
                .ToArray();

            if (progress is not null && progress.State != QuestState.InProgress)
                return (progress.State, amounts);

            // Поріг — той, що зафіксовано в прогресі на старті, як і в домені; без рядка — з конфіга
            var met = config.Objectives.Count > 0
                      && config.Objectives
                          .Select((o, i) => amounts[i] >= (progress?.Objectives.FirstOrDefault(p => p.Index == i)?.Required ?? o.Count))
                          .All(x => x);

            return (met ? QuestState.Completed : QuestState.InProgress, amounts);
        }
    }
}
