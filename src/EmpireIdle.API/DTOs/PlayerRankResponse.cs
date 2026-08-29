namespace EmpireIdle.API.DTOs
{
    /// <summary>Місце гравця з розкладкою рейтингу.</summary>
    public record PlayerRankResponse(
        int Rank,
        int Rating,
        double PowerScore,
        double DevelopmentScore,
        double ActivityScore,
        int MonstersDefeated,
        int BattlesWon,
        int QuestsCompleted,
        DateTime UpdatedAt);
}
