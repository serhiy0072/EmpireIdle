using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services.Config
{
    /// <summary>Опис квесту з quests.json. Додати квест = дописати JSON.</summary>
    public class QuestConfig
    {
        public string Key { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        public QuestScope Scope { get; set; } = QuestScope.Personal;
        public QuestWindow Window { get; set; } = QuestWindow.Chain;

        /// <summary>Ключ квесту, який має бути завершений раніше; null — доступний одразу.</summary>
        public string? Prerequisite { get; set; }

        /// <summary>Межі для Window=Event.</summary>
        public DateTime? ActiveFrom { get; set; }
        public DateTime? ActiveTo { get; set; }

        /// <summary>Квест відкритий зараз: вікно Event має межі, решта — завжди.</summary>
        public bool IsOpenAt(DateTime utcNow)
            => (ActiveFrom is not { } from || utcNow >= from)
               && (ActiveTo is not { } to || utcNow <= to);

        public List<QuestObjectiveConfig> Objectives { get; set; } = new();

        /// <summary>
        /// Нагорода за виконання. Для Scope=Server — кожному гравцю світу листом,
        /// незалежно від внеску (GDD §2.7, §8.4).
        /// </summary>
        public List<RewardConfig> Rewards { get; set; } = new();

        /// <summary>Бонус топу за внеском для Scope=Server — додається в той самий лист.</summary>
        public List<RewardTierConfig> RewardTiers { get; set; } = new();

        /// <summary>Очки вкладу, які клан отримує за завершення (Scope=Clan).</summary>
        public long ClanPoints { get; set; }
    }
}
