using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
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

    public sealed class GetQuestsQueryHandler : IRequestHandler<GetQuestsQuery, List<QuestView>>
    {
        private readonly IQuestRepository _questRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;

        public GetQuestsQueryHandler(IQuestRepository questRepository, IVillageRepository villageRepository, IServerContext serverContext,
            IUnitOfWork unitOfWork, TimeProvider timeProvider, GameCatalog catalog)
        {
            _questRepository = questRepository;
            _villageRepository = villageRepository;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
        }

        public async Task<List<QuestView>> Handle(GetQuestsQuery request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var progressByKey = (await _questRepository.GetAllAsync(request.PlayerId, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            await SyncThresholdsAsync(request.PlayerId, progressByKey, now, cancellationToken);

            // Ланцюжок відкривається завершенням, а не клеймом
            var unlocked = progressByKey.Values
                .Where(p => p.State != QuestState.InProgress)
                .Select(p => p.QuestKey)
                .ToHashSet();

            var views = new List<QuestView>();

            foreach (var config in _catalog.Quests.Values.Where(c => c.Scope == QuestScope.Personal))
            {
                if (config.Prerequisite is not null && !unlocked.Contains(config.Prerequisite))
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
        /// <summary>
        /// Підтягує порогові цілі до поточного стану села.
        /// Порогова ціль реагує лише на подію, тож гравець, який уже переріс віху,
        /// не побачив би її закритою до наступного апгрейду (GDD §15.1).
        /// </summary>
        private async Task SyncThresholdsAsync(Guid playerId, Dictionary<string, QuestProgress> progressByKey,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken);
            if (village is null)
                return;

            var levels = village.Buildings.ToDictionary(b => b.Type, b => b.Level.Value);
            var changed = false;

            foreach (var config in _catalog.Quests.Values.Where(q => q.Scope == QuestScope.Personal))
            {
                for (var i = 0; i < config.Objectives.Count; i++)
                {
                    var objective = config.Objectives[i];

                    // Синхронізуємо лише рівні будівель — інші порогові цілі
                    // (Power) з'являться у фазі 20 і додадуться сюди ж
                    if (objective.Mode != ObjectiveMode.Threshold
                        || objective.Type != nameof(BuildingUpgradeCompleted)
                        || objective.Target is null
                        || !levels.TryGetValue(objective.Target, out var level))
                        continue;

                    if (!progressByKey.TryGetValue(config.Key, out var progress))
                    {
                        // Не заводимо рядок, поки ціль не досягнута — інакше
                        // при перегляді списку створювався б прогрес на кожен квест
                        if (level < objective.Count)
                            continue;

                        progress = new QuestProgress(Guid.NewGuid(), playerId, _serverContext.ServerId,
                            config.Key, config.Objectives.Select(o => o.Count), utcNow);

                        await _questRepository.AddAsync(progress, cancellationToken);
                        progressByKey[config.Key] = progress;
                    }

                    if (progress.State != QuestState.InProgress)
                        continue;

                    progress.SetProgress(i, level, utcNow);
                    changed = true;
                }
            }

            if (changed)
                await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
