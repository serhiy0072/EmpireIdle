using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Map.ReadModels
{
    /// <summary>Вікно карти з межами: місцевість лише в межах світу, окупанти й табори — з бази.</summary>
    public record MapAreaView(int MinX, int MinY, int MaxX, int MaxY, List<MapTerrainTile> Terrain, List<MapAreaOccupant> Occupants,
        List<CampOnMap> Camps);

    /// <summary>Клітина місцевості — обчислюється з сіду світу, у БД не зберігається.</summary>
    public record MapTerrainTile(int X, int Y, string Type, bool Passable, bool Habitable);

    /// <summary>
    /// Окупант клітини для огляду мапи. Для монстра — тип і рівень:
    /// клієнт малює різних істот, не ходячи за деталями кожної клітини.
    /// Для кланової споруди — клан, його тег і коли вона запрацює: клієнт
    /// малює радіус і відрізняє свою територію від чужої. Name — ім'я монстра з каталогу
    /// або тег клану споруди.
    /// </summary>
    public record MapAreaOccupant(int X, int Y, MapOccupantType OccupantType, Guid OccupantId, string? Name,
        string? MonsterType, int? MonsterLevel, Guid? ClanId, string? ClanTag, DateTime? ReadyAt);
}
