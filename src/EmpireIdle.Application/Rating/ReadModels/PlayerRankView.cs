namespace EmpireIdle.Application.Rating.ReadModels
{
    /// <summary>Місце гравця з розкладкою, звідки взявся рейтинг.</summary>
    public record PlayerRankView(
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
