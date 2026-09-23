using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Combat;

/// <summary>Три кошики втрат після бою.</summary>
public record CasualtySplit(
    Dictionary<UnitStackKey, int> Wounded,
    Dictionary<UnitStackKey, int> Recoverable,
    Dictionary<UnitStackKey, int> Dead);
