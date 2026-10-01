namespace EmpireIdle.Application.Map.ReadModels
{
    /// <summary>Дані окупанта клітини. ShieldUntil — щит села після падіння, лише поки діє.</summary>
    public record MapCellOccupant(
        string OccupantType,
        Guid OccupantId,
        string? OccupantName,
        int? MonsterLevel,
        Dictionary<string, int>? MonsterUnits,
        DateTime? ShieldUntil = null);

    /// <summary>Клітина цілком: місцевість (обчислюється з сіду світу) і окупант, якщо є.</summary>
    public record MapCellView(
        int X,
        int Y,
        string TerrainType,
        bool Passable,
        bool Habitable,
        double MoveCost,
        MapCellOccupant? Occupant);
}
