namespace EmpireIdle.Application.Map.ReadModels
{
    /// <summary>Дані окупанта клітини.</summary>
    public record MapCellOccupant(
        string OccupantType,
        Guid OccupantId,
        string? OccupantName,
        int? MonsterLevel,
        Dictionary<string, int>? MonsterUnits);
}
