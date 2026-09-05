namespace EmpireIdle.Application.Rating.ReadModels
{
    /// <summary>Рядок топу.</summary>
    public record LeaderboardEntry(
        int Rank,
        Guid PlayerId,
        string PlayerName,
        int Rating,
        double Power);
}
