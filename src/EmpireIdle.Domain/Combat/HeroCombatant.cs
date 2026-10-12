namespace EmpireIdle.Domain.Combat;

/// <summary>
/// Герой як учасник армійського бою (варіант B, рішення 08.10.2026): не лише множник військ,
/// а окремий боєць зі своєю силою й здоров'ям.
/// </summary>
/// <param name="Power">Атака героя, коли його сторона атакує, і захист — коли обороняється.</param>
/// <param name="Health">Здоров'я героя; шкода від цього значення (з HeroHealthScale) і вище — поранення.</param>
public record HeroCombatant(Guid HeroId, double Power, double Health);
