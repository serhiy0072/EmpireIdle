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

        public List<QuestObjectiveConfig> Objectives { get; set; } = new();

        /// <summary>Нагорода для Scope=Personal.</summary>
        public List<RewardConfig> Rewards { get; set; } = new();

        /// <summary>Нагорода за рангом для Scope=Server.</summary>
        public List<RewardTierConfig> RewardTiers { get; set; } = new();
    }
}
