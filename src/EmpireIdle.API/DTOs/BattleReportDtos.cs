namespace EmpireIdle.API.DTOs;

/// <summary>Звіт про бій із деталями по типах юнітів.</summary>
/// <param name="TamedBeastKey">Звір, якого дала перемога з наміром «Приручити» (GDD §5.10).</param>
/// <param name="HeroExperience">Досвід, який перемога над монстром додала до запасу героїв (GDD §6.1); 0 — не дала.</param>
public record BattleReportResponse(
    Guid Id,
    Guid MarchId,
    int X, int Y,
    string TerrainType,
    string TargetName,
    int TargetLevel,
    bool Won,
    double AttackerPower,
    double DefenderPower,
    DateTime FoughtAt,
    bool IsRead,
    List<BattleReportLineResponse> Lines,
    string? TamedBeastKey,
    long HeroExperience);

/// <summary>Що сталося з конкретним типом юнітів.</summary>
public record BattleReportLineResponse(
    string UnitType,
    int Sent,
    int Survived,
    int Wounded,
    int Recoverable,
    int Dead);
