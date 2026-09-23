using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Map.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dev.Commands
{
    /// <summary>
    /// Перегенерація мапи для розробки після зміни насіння чи ваг місцевості:
    /// монстри зникають і засіваються заново за поточною щільністю, села, що
    /// опинились на воді чи скелях, переселяються на найближчу придатну клітину.
    /// Поза Development маршруту до команди не існує.
    /// </summary>
    public record ResetMapCommand(int ServerId) : IRequest<ResetMapResult>;

    public record ResetMapResult(int MonstersRemoved, int VillagesRelocated, int MonstersSpawned);

    internal sealed class ResetMapCommandHandler : IRequestHandler<ResetMapCommand, ResetMapResult>
    {
        /// <summary>Скільки прогонів спавнера робимо: кожен обмежений своїм MaxSpawnsPerRun.</summary>
        private const int MaxSpawnRuns = 100;

        /// <summary>Як далеко шукати придатну клітину для переселення.</summary>
        private const int RelocationRadius = 30;

        private readonly IMonsterRepository _monsterRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IVillageRepository _villageRepository;
        private readonly TerrainGenerator _terrain;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMediator _mediator;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ResetMapCommandHandler> _logger;

        public ResetMapCommandHandler(
            IMonsterRepository monsterRepository,
            IMapRepository mapRepository,
            IVillageRepository villageRepository,
            TerrainGenerator terrain,
            IUnitOfWork unitOfWork,
            IMediator mediator,
            TimeProvider timeProvider,
            ILogger<ResetMapCommandHandler> logger)
        {
            _monsterRepository = monsterRepository;
            _mapRepository = mapRepository;
            _villageRepository = villageRepository;
            _terrain = terrain;
            _unitOfWork = unitOfWork;
            _mediator = mediator;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<ResetMapResult> Handle(ResetMapCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Монстри — разом із клітинами, інакше клітина лишилась би зайнятою примарою
            var monsters = await _monsterRepository.GetAllAsync(request.ServerId, cancellationToken);

            foreach (var monster in monsters)
            {
                var cell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Monster, monster.Id, cancellationToken);
                if (cell is not null)
                    _mapRepository.Remove(cell);

                _monsterRepository.Remove(monster);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var relocated = 0;

            // Список читається без трекінгу — для переселення село береться заново, відстежуваним
            foreach (var snapshot in await _villageRepository.GetAllWithBuildingsAsync(cancellationToken))
            {
                if (_terrain.IsHabitable(request.ServerId, snapshot.X, snapshot.Y))
                    continue;

                var village = await _villageRepository.GetByPlayerIdAsync(snapshot.PlayerId, cancellationToken);
                if (village is null)
                    continue;

                var spot = await FindHabitableAsync(request.ServerId, village.X, village.Y, cancellationToken);

                if (spot is null)
                {
                    _logger.LogWarning("Village {VillageId} at ({X},{Y}) has no habitable cell within {Radius}",
                        village.Id, village.X, village.Y, RelocationRadius);
                    continue;
                }

                var oldCell = await _mapRepository.GetByOccupantAsync(MapOccupantType.Village, village.Id, cancellationToken);
                if (oldCell is not null)
                    _mapRepository.Remove(oldCell);

                village.RelocateTo(spot.Value.X, spot.Value.Y, now);
                await _mapRepository.AddAsync(
                    new MapCell(Guid.NewGuid(), request.ServerId, spot.Value.X, spot.Value.Y, MapOccupantType.Village, village.Id),
                    cancellationToken);

                // Кожне село окремою транзакцією: наступний пошук має бачити зайняту клітину
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                relocated++;
            }

            var before = await _monsterRepository.CountAsync(request.ServerId, cancellationToken);

            for (var run = 0; run < MaxSpawnRuns; run++)
            {
                var countBefore = await _monsterRepository.CountAsync(request.ServerId, cancellationToken);
                await _mediator.Send(new SpawnMonstersCommand(request.ServerId), cancellationToken);

                if (await _monsterRepository.CountAsync(request.ServerId, cancellationToken) == countBefore)
                    break; // ціль досягнута або місця немає
            }

            var spawned = await _monsterRepository.CountAsync(request.ServerId, cancellationToken) - before;

            _logger.LogInformation("Map reset on server {ServerId}: {Removed} monsters removed, {Relocated} villages relocated, {Spawned} monsters spawned",
                request.ServerId, monsters.Count, relocated, spawned);

            return new ResetMapResult(monsters.Count, relocated, spawned);
        }

        /// <summary>Спіраль від старої позиції: найближча придатна й вільна клітина.</summary>
        private async Task<(int X, int Y)?> FindHabitableAsync(int serverId, int fromX, int fromY, CancellationToken cancellationToken)
        {
            for (var ring = 1; ring <= RelocationRadius; ring++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    for (var dy = -ring; dy <= ring; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                            continue;

                        var x = fromX + dx;
                        var y = fromY + dy;

                        if (!_terrain.IsInBounds(x, y) || !_terrain.IsHabitable(serverId, x, y))
                            continue;

                        if (!await _mapRepository.IsOccupiedAsync(serverId, x, y, cancellationToken))
                            return (x, y);
                    }
                }
            }

            return null;
        }
    }
}
