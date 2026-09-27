using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>Ділянка карти навколо центру: місцевість і зайняті клітини.</summary>
    public record GetMapAreaQuery(int ServerId, int CenterX, int CenterY, int Radius) : IRequest<MapAreaView>;

    /// <summary>Вікно карти з межами: місцевість лише в межах світу, окупанти — з бази.</summary>
    public record MapAreaView(int MinX, int MinY, int MaxX, int MaxY, List<MapTerrainTile> Terrain, List<MapAreaOccupant> Occupants);

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

    public sealed class GetMapAreaQueryHandler : IRequestHandler<GetMapAreaQuery, MapAreaView>
    {
        public const int MaxRadius = 25; // 51×51 клітин — щоб не вивантажити пів світу

        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRepository _clanRepository;
        private readonly TerrainGenerator _terrain;
        private readonly GameCatalog _catalog;

        public GetMapAreaQueryHandler(IMapRepository mapRepository, IMonsterRepository monsterRepository,
            IClanStructureRepository structureRepository, IClanRepository clanRepository,
            TerrainGenerator terrain, GameCatalog catalog)
        {
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _structureRepository = structureRepository;
            _clanRepository = clanRepository;
            _terrain = terrain;
            _catalog = catalog;
        }

        public async Task<MapAreaView> Handle(GetMapAreaQuery request, CancellationToken cancellationToken)
        {
            if (request.Radius < 0 || request.Radius > MaxRadius)
                throw new ArgumentOutOfRangeException(nameof(request.Radius), $"Radius must be between 0 and {MaxRadius}.");

            var (minX, minY) = (request.CenterX - request.Radius, request.CenterY - request.Radius);
            var (maxX, maxY) = (request.CenterX + request.Radius, request.CenterY + request.Radius);

            var occupants = await OccupantsAsync(request.ServerId, minX, minY, maxX, maxY, cancellationToken);

            return new MapAreaView(minX, minY, maxX, maxY, Terrain(request.ServerId, minX, minY, maxX, maxY), occupants);
        }

        private List<MapTerrainTile> Terrain(int serverId, int minX, int minY, int maxX, int maxY)
        {
            var tiles = new List<MapTerrainTile>();

            for (var x = minX; x <= maxX; x++)
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_terrain.IsInBounds(x, y))
                        continue;

                    var cell = _terrain.GetTerrain(serverId, x, y);
                    tiles.Add(new MapTerrainTile(x, y, cell.Type, cell.Passable, cell.Habitable));
                }

            return tiles;
        }

        private async Task<List<MapAreaOccupant>> OccupantsAsync(int serverId, int minX, int minY, int maxX, int maxY,
            CancellationToken cancellationToken)
        {
            var cells = await _mapRepository.GetAreaAsync(serverId, minX, minY, maxX, maxY, cancellationToken);

            // Монстри — одним запитом на всі клітини ділянки, а не по одному на клітину
            var monsterIds = cells
                .Where(c => c.OccupantType == MapOccupantType.Monster)
                .Select(c => c.OccupantId)
                .ToList();

            var monsters = monsterIds.Count == 0
                ? []
                : (await _monsterRepository.GetByIdsAsync(monsterIds, cancellationToken)).ToDictionary(m => m.Id);

            // Споруди й теги їхніх кланів — теж пакетом
            var structures = cells.Any(c => c.OccupantType == MapOccupantType.ClanStructure)
                ? (await _structureRepository.GetInAreaAsync(minX, minY, maxX, maxY, cancellationToken))
                    .ToDictionary(s => s.Id)
                : [];

            var clans = structures.Count == 0
                ? []
                : await _clanRepository.GetCardsAsync(structures.Values.Select(s => s.ClanId).Distinct().ToList(), cancellationToken);

            return cells
                .Select(c =>
                {
                    // Монстр міг зникнути між двома запитами — клітина тоді без типу, а не помилка
                    var monster = c.OccupantType == MapOccupantType.Monster ? monsters.GetValueOrDefault(c.OccupantId) : null;
                    var structure = c.OccupantType == MapOccupantType.ClanStructure ? structures.GetValueOrDefault(c.OccupantId) : null;

                    var clanTag = structure is null ? null : clans.GetValueOrDefault(structure.ClanId)?.Tag;
                    var name = monster is null ? clanTag : _catalog.Monsters.GetValueOrDefault(monster.Type)?.DisplayName;

                    return new MapAreaOccupant(c.X, c.Y, c.OccupantType, c.OccupantId, name, monster?.Type, monster?.Level,
                        structure?.ClanId, clanTag, structure?.CompletesAt);
                })
                .ToList();
        }
    }
}
