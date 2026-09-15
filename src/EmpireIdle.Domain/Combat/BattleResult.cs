namespace EmpireIdle.Domain.Combat;

/// <summary>Результат бою.</summary>
public record BattleResult(
    bool AttackerWon,
    double AttackerPower,
    double DefenderPower,
    Dictionary<string, int> AttackerLosses,
    Dictionary<string, int> DefenderLosses);
