namespace EmpireIdle.Domain.Combat;

/// <summary>
/// Результат бою разом із розкладкою втрат атакувальника.
/// </summary>
public record BattleOutcome(BattleResult Battle, CasualtySplit AttackerCasualties);
