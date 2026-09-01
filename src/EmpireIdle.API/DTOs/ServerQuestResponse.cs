namespace EmpireIdle.API.DTOs
{
    /// <summary>Серверний квест із внеском і рангом гравця.</summary>
    public record ServerQuestResponse(
        string Key,
        string DisplayName,
        long Total,
        long Target,
        string State,
        DateTime? CompletedAt,
        long MyContribution,
        int MyRank);
}
