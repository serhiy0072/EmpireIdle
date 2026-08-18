using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services
{
    /// <summary>Одна ціль квесту.</summary>
    public class QuestObjectiveConfig
    {
        /// <summary>Ім'я доменної події (BuildingUpgradeCompleted, MonsterDefeated…).</summary>
        public string Type { get; set; } = null!;

        /// <summary>Уточнення цілі: ключ будівлі, тип юніта. null — будь-який.</summary>
        public string? Target { get; set; }

        public int Count { get; set; }

        public ObjectiveMode Mode { get; set; } = ObjectiveMode.Accumulate;
    }
}
