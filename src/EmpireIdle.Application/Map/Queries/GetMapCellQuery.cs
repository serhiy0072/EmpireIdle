using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>Дані окупанта клітини.</summary>
    public record MapCellOccupant(string OccupantType,Guid OccupantId,string? OccupantName,int? MonsterLevel,Dictionary<string, int>? MonsterUnits);

    /// <summary>Хто стоїть на клітині.</summary>
    public record GetMapCellQuery(int ServerId, int X, int Y) : IRequest<MapCellOccupant?>;

    public sealed class GetMapCellQueryHandler : IRequestHandler<GetMapCellQuery, MapCellOccupant?>
    {
        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly MonsterArmyBuilder _armyBuilder;

        public GetMapCellQueryHandler(IMapRepository mapRepository,IMonsterRepository monsterRepository,IVillageRepository villageRepository,MonsterArmyBuilder armyBuilder)
        {
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _villageRepository = villageRepository;
            _armyBuilder = armyBuilder;
        }

        public async Task<MapCellOccupant?> Handle(GetMapCellQuery request, CancellationToken cancellationToken)
        {
            var cells = await _mapRepository.GetAreaAsync(request.ServerId, request.X, request.Y, request.X, request.Y, cancellationToken);

            var cell = cells.FirstOrDefault();
            if (cell is null)
                return null;

            if (cell.OccupantType == MapOccupantType.Monster)
            {
                var monster = await _monsterRepository.GetByIdAsync(cell.OccupantId, cancellationToken);
                if (monster is null)
                    return null;

                return new MapCellOccupant("Monster", monster.Id, monster.Type,monster.Level,_armyBuilder.BuildArmy(monster.Type, monster.Level));
            }

            var village = await _villageRepository.GetByIdAsync(cell.OccupantId, cancellationToken);
            return new MapCellOccupant("Village", cell.OccupantId, village?.Name, null, null);
        }
    }
}
