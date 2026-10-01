using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Quests.Services
{
    /// <summary>
    /// Порогові цілі (рівень будівлі): подія їх рухає лише в момент апгрейду, тож гравець,
    /// що вже переріс віху до відкриття квесту, інакше не побачив би її закритою (GDD §15.1).
    ///
    /// Два режими, щоб запит лишався запитом: <see cref="CurrentValue"/> — обчислення
    /// в пам'яті для списку квестів, <see cref="SyncAsync"/> — запис для команди, що
    /// забирає нагороду.
    /// </summary>
    public sealed class QuestThresholds
    {
        private readonly IQuestRepository _questRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly IServerContext _serverContext;

        public QuestThresholds(IQuestRepository questRepository, IVillageRepository villageRepository,
            IServerContext serverContext)
        {
            _questRepository = questRepository;
            _villageRepository = villageRepository;
            _serverContext = serverContext;
        }

        /// <summary>Рівні будівель села гравця; порожньо — села немає.</summary>
        public async Task<IReadOnlyDictionary<string, int>> LevelsAsync(Guid playerId, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdReadOnlyAsync(playerId, cancellationToken);

            return village is null
                ? new Dictionary<string, int>()
                : village.Buildings.ToDictionary(b => b.Type, b => b.Level.Value);
        }

        /// <summary>Поточне значення порогової цілі; null — ціль не порогова або будівлі немає.</summary>
        public static int? CurrentValue(QuestObjectiveConfig objective, IReadOnlyDictionary<string, int> levels)
            => objective.Mode == ObjectiveMode.Threshold
               && objective.Type == nameof(BuildingUpgradeCompleted)
               && objective.Target is { } target
               && levels.TryGetValue(target, out var level)
                ? level
                : null;

        /// <summary>
        /// Записує досягнуті пороги одного квесту. Рядок прогресу заводиться лише тоді, коли
        /// поріг уже досягнуто, — інакше кожен перегляд плодив би рядки на всі квести.
        /// </summary>
        /// <returns>Прогрес після синхронізації; null — рядка немає й заводити нічого.</returns>
        public async Task<QuestProgress?> SyncAsync(Guid playerId, QuestConfig config, QuestProgress? progress,
            DateTime utcNow, CancellationToken cancellationToken)
        {
            if (progress is not null && progress.State != QuestState.InProgress)
                return progress;

            var levels = await LevelsAsync(playerId, cancellationToken);

            for (var i = 0; i < config.Objectives.Count; i++)
            {
                if (CurrentValue(config.Objectives[i], levels) is not int level)
                    continue;

                if (progress is null)
                {
                    if (level < config.Objectives[i].Count)
                        continue;

                    progress = new QuestProgress(Guid.NewGuid(), playerId, _serverContext.ServerId,
                        config.Key, config.Objectives.Select(o => o.Count), utcNow);

                    await _questRepository.AddAsync(progress, cancellationToken);
                }

                progress.SetProgress(i, level, utcNow);
            }

            return progress;
        }
    }
}
