using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Quests
{
    /// <summary>
    /// Просуває прогрес квестів за сигналом події.
    /// Прогрес створюється лениво — рядок з'являється при першому влучанні,
    /// а не для кожного квесту кожному гравцю.
    /// </summary>
    public class QuestProgressTracker
    {
        private readonly IQuestRepository _questRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<QuestProgressTracker> _logger;

        public QuestProgressTracker(
            IQuestRepository questRepository,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<QuestProgressTracker> logger)
        {
            _questRepository = questRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task TrackAsync(QuestSignal signal, DateTime utcNow, CancellationToken cancellationToken)
        {
            var candidates = _catalog.Quests.Values
                .Where(q => q.Scope == QuestScope.Personal && IsOpen(q, utcNow))
                .Where(q => q.Objectives.Any(o => Matches(o, signal)))
                .ToList();

            if (candidates.Count == 0)
                return;

            var existing = (await _questRepository.GetAllAsync(signal.PlayerId, cancellationToken))
                .ToDictionary(p => p.QuestKey);

            var claimed = existing.Values
                .Where(p => p.State == QuestState.Claimed)
                .Select(p => p.QuestKey)
                .ToHashSet();

            var touched = false;

            foreach (var config in candidates)
            {
                // Квест за замкненим пререквізитом не починається
                if (config.Prerequisite is not null && !claimed.Contains(config.Prerequisite))
                    continue;

                if (!existing.TryGetValue(config.Key, out var progress))
                {
                    progress = new QuestProgress(Guid.NewGuid(), signal.PlayerId, config.Key,
                        config.Objectives.Select(o => o.Count), utcNow);

                    await _questRepository.AddAsync(progress, cancellationToken);
                    existing[config.Key] = progress;
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

                    touched = true;
                }
            }

            if (touched)
                await _unitOfWork.SaveChangesAsync(cancellationToken);
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
