using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Quests.Services
{
    /// <summary>Квест, як його бачить гравець: стан і лічильники з урахуванням порогів.</summary>
    public sealed record QuestBoardEntry(QuestConfig Config, QuestState State, IReadOnlyList<int> Amounts);

    /// <summary>
    /// Дошка квестів гравця — одне джерело правди для списку й «Зібрати все»: які квести
    /// відкриті й у якому вони стані. Лише обчислення в пам'яті, нічого не пише.
    /// </summary>
    public static class QuestBoard
    {
        /// <summary>
        /// Відкриті особисті квести гравця. Ланцюжок відкривається завершенням, а не клеймом.
        /// Поріг закриває ланку лише тоді, коли вона сама вже відкрита: інакше ратуша 3
        /// відкрила б третій квест повз перший.
        /// </summary>
        public static List<QuestBoardEntry> Visible(IEnumerable<QuestConfig> quests,
            IReadOnlyDictionary<string, QuestProgress> progressByKey, IReadOnlyDictionary<string, int> levels, DateTime now)
        {
            var personal = quests.Where(c => c.Scope == QuestScope.Personal).ToList();

            // Стан кожного квесту з урахуванням порогів — один раз, бо від нього ж залежить і ланцюжок
            var effective = personal.ToDictionary(c => c.Key, c => Evaluate(c, progressByKey.GetValueOrDefault(c.Key), levels));

            var configByKey = personal.ToDictionary(c => c.Key);
            var doneByKey = new Dictionary<string, bool>();

            bool IsDone(string key)
            {
                if (doneByKey.TryGetValue(key, out var known))
                    return known;

                // Запис до рекурсії — захист від циклу в конфігу
                doneByKey[key] = false;

                if (!configByKey.TryGetValue(key, out var quest))
                    return false;

                var done = progressByKey.GetValueOrDefault(key) is { State: not QuestState.InProgress }
                           || (effective[key].State != QuestState.InProgress
                               && (quest.Prerequisite is null || IsDone(quest.Prerequisite)));

                doneByKey[key] = done;

                return done;
            }

            return personal
                .Where(c => (c.Prerequisite is null || IsDone(c.Prerequisite)) && c.IsOpenAt(now))
                .Select(c => new QuestBoardEntry(c, effective[c.Key].State, effective[c.Key].Amounts))
                .ToList();
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
