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
    /// Для кланової споруди — клан, його тег і коли вона запрацює: клієнт
    /// малює радіус і відрізняє свою територію від чужої.
    /// </summary>
    public record MapAreaOccupant(int X, int Y, MapOccupantType OccupantType, Guid OccupantId, string? MonsterType, int? MonsterLevel,
        Guid? ClanId = null, string? ClanTag = null, DateTime? ReadyAt = null);

    public sealed class GetMapAreaQueryHandler : IRequestHandler<GetMapAreaQuery, List<MapAreaOccupant>>
    {
        private const int MaxRadius = 25; // 51×51 клітин — щоб не вивантажити пів світу

        private readonly IMapRepository _mapRepository;
        private readonly IMonsterRepository _monsterRepository;
        private readonly IClanStructureRepository _structureRepository;
        private readonly IClanRepository _clanRepository;

        public GetMapAreaQueryHandler(IMapRepository mapRepository, IMonsterRepository monsterRepository,
            IClanStructureRepository structureRepository, IClanRepository clanRepository)
        {
            _mapRepository = mapRepository;
            _monsterRepository = monsterRepository;
            _structureRepository = structureRepository;
            _clanRepository = clanRepository;
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

            // Споруди й теги їхніх кланів — теж пакетом
            var structures = cells.Any(c => c.OccupantType == MapOccupantType.ClanStructure)
                ? (await _structureRepository.GetInAreaAsync(
                    request.CenterX - request.Radius, request.CenterY - request.Radius,
                    request.CenterX + request.Radius, request.CenterY + request.Radius, cancellationToken))
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

                    return new MapAreaOccupant(c.X, c.Y, c.OccupantType, c.OccupantId, monster?.Type, monster?.Level,
                        structure?.ClanId,
                        structure is null ? null : clans.GetValueOrDefault(structure.ClanId)?.Tag,
                        structure?.CompletesAt);
                })
                .ToList();
        }
    }
}
