using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.ServerQuests.ReadModels
{
    /// <summary>Серверний квест очима конкретного гравця.</summary>
    public record ServerQuestView(
        string Key,
        string DisplayName,
        long Total,
        long Target,
        QuestState State,
        DateTime? CompletedAt,
        long MyContribution,
        int MyRank);
}
