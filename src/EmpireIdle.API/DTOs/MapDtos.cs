namespace EmpireIdle.API.DTOs;

/// <summary>Ділянка карти: місцевість + зайняті клітини.</summary>
public record MapAreaResponse(
    int MinX, int MinY, int MaxX, int MaxY,
    List<MapTerrainCell> Terrain,
    List<MapOccupantCell> Occupants);

/// <summary>Клітина місцевості (обчислюється, у БД не зберігається).</summary>
public record MapTerrainCell(int X, int Y, string Type, bool Passable, bool Habitable);

/// <summary>Зайнята клітина: хто саме на ній стоїть.</summary>
public record MapOccupantCell(int X, int Y, string OccupantType, Guid OccupantId, string? Name);

/// <summary>Деталі однієї клітини.</summary>
public record MapCellDetailsResponse(
    int X, int Y,
    string TerrainType, bool Passable, bool Habitable, double MoveCost,
    string? OccupantType, Guid? OccupantId, string? OccupantName,
    int? MonsterLevel, Dictionary<string, int>? MonsterUnits);
