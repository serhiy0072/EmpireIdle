using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Application.Quests.ReadModels
{
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
}
