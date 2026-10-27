using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Quests.ReadModels;
using EmpireIdle.Application.Quests.Services;
using EmpireIdle.Domain.Services;
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
        private readonly LocaleTexts _texts;

        public GetQuestsQueryHandler(IQuestRepository questRepository, QuestThresholds thresholds,
            TimeProvider timeProvider, GameCatalog catalog, LocaleTexts texts)
        {
            _texts = texts;
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

            return QuestBoard.Visible(_catalog.Quests.Values, progressByKey, levels, now)
                .Select(entry => new QuestView(
                    entry.Config.Key,
                    _texts.Name("quest", entry.Config.Key, entry.Config.DisplayName),
                    entry.Config.Scope,
                    entry.Config.Window,
                    entry.State,
                    entry.Config.Objectives
                        .Select((o, i) => new QuestObjectiveView(o.Type, o.Target, entry.Amounts[i], o.Count))
                        .ToList(),
                    entry.Config.Rewards))
                .ToList();
        }
    }
}
