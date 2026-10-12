using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Combat
{
    /// <summary>Що бачить гравець перед відправкою армії.</summary>
    /// <param name="Verdict">Перемога гарантована, можлива чи неможлива — за межами удачі в бою.</param>
    /// <param name="WinChance">Шанс перемоги від 0 до 1.</param>
    /// <param name="AttackerPower">Умовна сила армії; лише проти монстра — чуже село так не розкривається.</param>
    /// <param name="TargetPower">Умовна сила монстра; null для інших цілей.</param>
    public record BattlePreviewResult(
        BattleOdds Odds,
        string TargetName,
        int TargetX,
        int TargetY,
        string Terrain,
        TimeSpan TravelTime,
        BattleVerdict Verdict,
        double WinChance,
        double? AttackerPower,
        double? TargetPower);
}
