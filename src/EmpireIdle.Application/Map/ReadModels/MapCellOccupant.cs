using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Map.ReadModels
{
    /// <summary>Дані окупанта клітини. ShieldUntil — щит села після падіння, лише поки діє.</summary>
    /// <param name="MonsterType">Тип монстра — за ним клієнт знає, чи монстр приручається (GDD §5.10).</param>
    /// <param name="MonsterPower">Умовна сила монстра в обороні на цій клітині.</param>
    public record MapCellOccupant(
        MapOccupantType OccupantType,
        Guid OccupantId,
        string? OccupantName,
        int? MonsterLevel,
        Dictionary<string, int>? MonsterUnits,
        DateTime? ShieldUntil = null,
        string? MonsterType = null,
        double? MonsterPower = null);

    /// <summary>
    /// Клітина цілком: місцевість (обчислюється з сіду світу) і окупант, якщо є, — пласко:
    /// поля окупанта порожні, коли клітина вільна.
    /// </summary>
    public record MapCellView(
        int X,
        int Y,
        string TerrainType,
        bool Passable,
        bool Habitable,
        double MoveCost,
        MapOccupantType? OccupantType,
        Guid? OccupantId,
        string? OccupantName,
        int? MonsterLevel,
        Dictionary<string, int>? MonsterUnits,
        DateTime? ShieldUntil,
        string? MonsterType,
        double? MonsterPower)
    {
        public static MapCellView Of(int x, int y, string terrainType, bool passable, bool habitable, double moveCost,
            MapCellOccupant? occupant)
            => new(x, y, terrainType, passable, habitable, moveCost,
                occupant?.OccupantType, occupant?.OccupantId, occupant?.OccupantName, occupant?.MonsterLevel,
                occupant?.MonsterUnits, occupant?.ShieldUntil, occupant?.MonsterType, occupant?.MonsterPower);
    }
}
