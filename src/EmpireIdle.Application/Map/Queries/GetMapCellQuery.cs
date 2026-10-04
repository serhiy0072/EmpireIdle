using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.ReadModels;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>
    /// Деталі клітини поточного світу: місцевість і хто на ній стоїть. Для монстра —
    /// склад загону, щоб напад був вибором, а не лотереєю.
    /// </summary>
    public record GetMapCellQuery(int X, int Y) : IRequest<MapCellView>;

    public sealed class GetMapCellQueryHandler : IRequestHandler<GetMapCellQuery, MapCellView>
    {
        private readonly IServerContext _serverContext;
        private readonly TerrainGenerator _terrain;
        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly MonsterArmyBuilder _armyBuilder;
        private readonly TimeProvider _timeProvider;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRepository _clanRepository;

        public GetMapCellQueryHandler(IMapRepository mapRepository, IMonsterRepository monsterRepository,
            IVillageRepository villageRepository, MonsterArmyBuilder armyBuilder, TimeProvider timeProvider,
            IClanStructureRepository structureRepository, IClanRepository clanRepository,
            IServerContext serverContext, TerrainGenerator terrain)
        {
            _serverContext = serverContext;
            _terrain = terrain;
            _structureRepository = structureRepository;
            _clanRepository = clanRepository;
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _armyBuilder = armyBuilder;
            _timeProvider = timeProvider;
        }

        public async Task<MapCellView> Handle(GetMapCellQuery request, CancellationToken cancellationToken)
        {
            if (!_terrain.IsInBounds(request.X, request.Y))
                throw new RequirementNotMetException($"Cell ({request.X},{request.Y}) is outside the map.");

            var serverId = _serverContext.ServerId;
            var terrain = _terrain.GetTerrain(serverId, request.X, request.Y);
            var occupant = await OccupantAsync(serverId, request.X, request.Y, cancellationToken);

            return new MapCellView(request.X, request.Y, terrain.Type, terrain.Passable, terrain.Habitable, terrain.MoveCost,
                occupant);
        }

        private async Task<MapCellOccupant?> OccupantAsync(int serverId, int x, int y, CancellationToken cancellationToken)
        {
            var cells = await _mapRepository.GetAreaAsync(serverId, x, y, x, y, cancellationToken);

            var cell = cells.FirstOrDefault();
            if (cell is null)
                return null;

            if (cell.OccupantType == MapOccupantType.Monster)
            {
                var monster = (await _monsterRepository.GetByIdsAsync([cell.OccupantId], cancellationToken)).FirstOrDefault();
                if (monster is null)
                    return null;

                return new MapCellOccupant("Monster", monster.Id, monster.Type, monster.Level,
                    _armyBuilder.BuildArmy(monster.Type, monster.Level), MonsterType: monster.Type);
            }

            if (cell.OccupantType == MapOccupantType.ClanStructure)
            {
                var structure = await _structureRepository.GetByIdAsync(cell.OccupantId, cancellationToken);
                if (structure is null)
                    return null;

                // Назва споруди для гравця — тег її клану
                var clan = await _clanRepository.GetCardAsync(structure.ClanId, cancellationToken);

                return new MapCellOccupant("ClanStructure", structure.Id, clan?.Tag, null, null);
            }

            var village = await _villageRepository.GetByIdReadOnlyAsync(cell.OccupantId, cancellationToken);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return new MapCellOccupant("Village", cell.OccupantId, village?.Name, null, null,
                village is not null && village.IsShieldedAt(now) ? village.ShieldUntil : null);
        }
    }
}
