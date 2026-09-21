using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Combat;

/// <summary>Результат бою.</summary>
public record BattleResult(
    bool AttackerWon,
    double AttackerPower,
    double DefenderPower,
    Dictionary<UnitStackKey, int> AttackerLosses,
    Dictionary<UnitStackKey, int> DefenderLosses);
