namespace EmpireIdle.API.DTOs;

/// <summary>Бойова сила гравця з розкладкою по джерелах.</summary>
public record PowerResponse(
    double Total,
    double Army,
    double Hero,
    double Equipment,
    DateTime UpdatedAt);

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
