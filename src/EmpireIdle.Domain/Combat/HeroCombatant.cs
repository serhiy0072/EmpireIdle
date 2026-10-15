namespace EmpireIdle.Domain.Combat;

/// <summary>
/// Герой як учасник армійського бою (варіант B, рішення 08.10.2026): не лише множник військ,
/// а окремий боєць зі своєю силою й здоров'ям.
/// </summary>
/// <param name="Power">Атака героя, коли його сторона атакує, і захист — коли обороняється.</param>
/// <param name="Health">Здоров'я героя; шкода від цього значення (з HeroHealthScale) і вище — поранення.</param>
/// <param name="TroopPower">
/// Сила військам зі спорядження (UnitAttack у нападі, UnitDefense в обороні, × UnitStatPower): додається
/// до сили сторони, але не до частки героя в шкоді — це військо, а не сам герой.
/// </param>
/// <param name="Ward">Оберіг часткою: поріг поранення героя × (1 + Ward).</param>
public record HeroCombatant(Guid HeroId, double Power, double Health, double TroopPower = 0, double Ward = 0);
