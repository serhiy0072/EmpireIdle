namespace EmpireIdle.Domain.Combat;

/// <summary>Три кошики втрат після бою.</summary>
public record CasualtySplit(Dictionary<string, int> Wounded, Dictionary<string, int> Recoverable, Dictionary<string, int> Dead);
