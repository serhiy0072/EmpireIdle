using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Combat;

/// <summary>Результат бою.</summary>
/// <param name="AttackerWoundedHeroes">Герої атакувальника, чия частка шкоди дорівняла їхньому здоров'ю.</param>
/// <param name="DefenderWoundedHeroes">Те саме для захисника.</param>
public record BattleResult(
    bool AttackerWon,
    double AttackerPower,
    double DefenderPower,
    Dictionary<UnitStackKey, int> AttackerLosses,
    Dictionary<UnitStackKey, int> DefenderLosses,
    IReadOnlyList<Guid>? AttackerWoundedHeroes = null,
    IReadOnlyList<Guid>? DefenderWoundedHeroes = null)
{
    /// <summary>Чи поранено цього героя в бою — з будь-якого боку.</summary>
    public bool Wounded(Guid heroId)
        => (AttackerWoundedHeroes?.Contains(heroId) ?? false) || (DefenderWoundedHeroes?.Contains(heroId) ?? false);
}
