namespace EmpireIdle.API.DTOs
{
    /// <summary>Рядок серверного топу.</summary>
    public record LeaderboardEntryResponse(
        int Rank,
        Guid PlayerId,
        string PlayerName,
        int Rating,
        double Power);
}
