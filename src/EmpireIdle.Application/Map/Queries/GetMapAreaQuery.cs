using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using MediatR;

namespace EmpireIdle.Application.Map.Queries
{
    /// <summary>Зайняті клітини у вікні навколо центру.</summary>
    public record GetMapAreaQuery(int ServerId, int CenterX, int CenterY, int Radius) : IRequest<List<MapAreaOccupant>>;

    /// <summary>
    /// Окупант клітини для огляду мапи. Для монстра — тип і рівень:
    /// клієнт малює різних істот, не ходячи за деталями кожної клітини.
    /// </summary>
    public record MapAreaOccupant(int X, int Y, MapOccupantType OccupantType, Guid OccupantId, string? MonsterType, int? MonsterLevel);

    public sealed class GetMapAreaQueryHandler : IRequestHandler<GetMapAreaQuery, List<MapAreaOccupant>>
    {
        private const int MaxRadius = 25; // 51×51 клітин — щоб не вивантажити пів світу

        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;

        public GetMapAreaQueryHandler(IMapRepository mapRepository, IMonsterRepository monsterRepository)
        {
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
        }

        public async Task<List<MapAreaOccupant>> Handle(GetMapAreaQuery request, CancellationToken cancellationToken)
        {
            if (request.Radius < 0 || request.Radius > MaxRadius)
                throw new ArgumentOutOfRangeException(nameof(request.Radius), $"Radius must be between 0 and {MaxRadius}.");

            var cells = await _mapRepository.GetAreaAsync(
                request.ServerId,
                request.CenterX - request.Radius, request.CenterY - request.Radius,
                request.CenterX + request.Radius, request.CenterY + request.Radius,
                cancellationToken);

            // Монстри — одним запитом на всі клітини ділянки, а не по одному на клітину
            var monsterIds = cells
                .Where(c => c.OccupantType == MapOccupantType.Monster)
                .Select(c => c.OccupantId)
                .ToList();

            var monsters = monsterIds.Count == 0
                ? []
                : (await _monsterRepository.GetByIdsAsync(monsterIds, cancellationToken)).ToDictionary(m => m.Id);

            return cells
                .Select(c =>
                {
                    // Монстр міг зникнути між двома запитами — клітина тоді без типу, а не помилка
                    var monster = c.OccupantType == MapOccupantType.Monster ? monsters.GetValueOrDefault(c.OccupantId) : null;
                    return new MapAreaOccupant(c.X, c.Y, c.OccupantType, c.OccupantId, monster?.Type, monster?.Level);
                })
                .ToList();
        }
    }
}
