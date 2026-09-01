using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests.Tracking
{
    /// <summary>
    /// Просуває прогрес квестів за сигналом події.
    /// Прогрес створюється лениво — рядок з'являється при першому влучанні,
    /// а не для кожного квесту кожному гравцю.
    /// Не зберігає: транзакцією володіє той, хто її відкрив (OutboxProcessor).
    /// </summary>
    public class QuestProgressTracker
    {
        private readonly IQuestRepository _questRepository;
        private readonly IServerContext _serverContext;
        private readonly IServerQuestRepository _serverQuestRepository;
        private readonly GameCatalog _catalog;
        private readonly ILogger<QuestProgressTracker> _logger;

        public QuestProgressTracker(
            IQuestRepository questRepository,
            IServerContext serverContext,
            IServerQuestRepository serverQuestRepository,
            GameCatalog catalog,
            ILogger<QuestProgressTracker> logger)
        {
            _questRepository = questRepository;
            _serverContext = serverContext;
            _serverQuestRepository = serverQuestRepository;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task TrackAsync(QuestSignal signal, DateTime utcNow, CancellationToken cancellationToken)
        {
            await TrackPersonalAsync(signal, utcNow, cancellationToken);
            await TrackServerAsync(signal, utcNow, cancellationToken);
        }

        public async Task TrackPersonalAsync(QuestSignal signal, DateTime utcNow, CancellationToken cancellationToken)
        {
            var candidates = _catalog.Quests.Values
                .Where(q => q.Scope == QuestScope.Personal && IsOpen(q, utcNow))
                .Where(q => q.Objectives.Any(o => Matches(o, signal)))
                .ToList();

            if (candidates.Count == 0)
                return;

            // Читаємо лише кандидатів і їхні пререквізити — решта прогресу нам не потрібна
            var keys = candidates.Select(c => c.Key)
                .Concat(candidates.Where(c => c.Prerequisite is not null).Select(c => c.Prerequisite!))
                .ToHashSet();

            var loaded = (await _questRepository.GetByKeysAsync(signal.PlayerId, keys, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            foreach (var config in candidates)
            {
                // Ланцюжок відкривається завершенням, а не клеймом:
                // незабрана нагорода не має блокувати прогресію
                if (config.Prerequisite is not null &&
                    (!loaded.TryGetValue(config.Prerequisite, out var previous) ||
                     previous.State == QuestState.InProgress))
                    continue;

                if (!loaded.TryGetValue(config.Key, out var progress))
                {
                    progress = new QuestProgress(Guid.NewGuid(), signal.PlayerId, _serverContext.ServerId,
                        config.Key, config.Objectives.Select(o => o.Count), utcNow);

                    await _questRepository.AddAsync(progress, cancellationToken);
                    loaded[config.Key] = progress;

                    _logger.LogDebug("Quest {QuestKey} started for player {PlayerId}", config.Key, signal.PlayerId);
                }

                if (progress.State != QuestState.InProgress)
                    continue;

                for (var i = 0; i < config.Objectives.Count; i++)
                {
                    var objective = config.Objectives[i];
                    if (!Matches(objective, signal))
                        continue;

                    if (objective.Mode == ObjectiveMode.Threshold)
                    {
                        // Порогова ціль читає стан; подія без стану її не рухає
                        if (signal.CurrentValue is { } current)
                            progress.SetProgress(i, current, utcNow);
                    }
                    else
                    {
                        progress.Advance(i, signal.Increment, utcNow);
                    }
                }
            }
        }

        /// <summary>
        /// Записує внесок у серверні квести.
        ///
        /// Пишемо лише у свій рядок гравця — спільний Total збирає джоб.
        /// Інкрементувати його тут означало б зробити один рядок точкою
        /// конкуренції для всього світу.
        /// </summary>
        private async Task TrackServerAsync(QuestSignal signal, DateTime utcNow, CancellationToken cancellationToken)
        {
            var candidates = _catalog.Quests.Values
                .Where(q => q.Scope == QuestScope.Server && IsOpen(q, utcNow))
                .Where(q => q.Objectives.Any(o => Matches(o, signal)))
                .ToList();

            foreach (var config in candidates)
            {
                var progress = await _serverQuestRepository.GetProgressAsync(config.Key, cancellationToken);

                // Завершений квест внесків більше не приймає
                if (progress is not null && progress.State != QuestState.InProgress)
                    continue;

                // Порогові цілі в серверних квестах не мають сенсу: внесок
                // накопичується від усіх, а поточний стан належить одному гравцю
                var amount = config.Objectives
                    .Where(o => Matches(o, signal) && o.Mode != ObjectiveMode.Threshold)
                    .Sum(_ => (long)signal.Increment);

                if (amount <= 0)
                    continue;

                var contribution = await _serverQuestRepository.GetContributionAsync(
                    config.Key, signal.PlayerId, cancellationToken);

                if (contribution is null)
                {
                    contribution = new ServerQuestContribution(
                        Guid.NewGuid(), _serverContext.ServerId, config.Key, signal.PlayerId);

                    await _serverQuestRepository.AddContributionAsync(contribution, cancellationToken);
                }

                contribution.Add(amount, utcNow);
            }
        }

        /// <summary>Ціль реагує на подію, якщо збігся тип і (за наявності) уточнення.</summary>
        private static bool Matches(QuestObjectiveConfig objective, QuestSignal signal)
            => objective.Type == signal.EventType
               && (objective.Target is null || objective.Target == signal.Target);

        /// <summary>Квест доступний зараз: вікно Event має межі, решта — завжди.</summary>
        private static bool IsOpen(QuestConfig config, DateTime utcNow)
            => (config.ActiveFrom is not { } from || utcNow >= from)
               && (config.ActiveTo is not { } to || utcNow <= to);


    }
}
