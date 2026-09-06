using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Services
{    /// <summary>Що бачить гравець перед відправкою армії.</summary>
    public record BattlePreviewResult(
        BattleOdds Odds,
        string TargetName,
        int TargetX,
        int TargetY,
        string Terrain,
        TimeSpan TravelTime);
}
