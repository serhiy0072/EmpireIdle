using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Map.Commands
{
    /// <summary>Доповнює популяцію монстрів до цільової щільності на вказаному сервері.</summary>
    public record SpawnMonstersCommand(int ServerId) : IRequest;

    public sealed class SpawnMonstersCommandHandler : IRequestHandler<SpawnMonstersCommand>
    {
        private const int MaxSpawnsPerRun = 50;  // не засівати всю карту одним прогоном

        private readonly IMonsterRepository _monsterRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerRepository _serverRepository;
        private readonly MonsterSpawner _spawner;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SpawnMonstersCommandHandler> _logger;

        public SpawnMonstersCommandHandler(
            IMonsterRepository monsterRepository,
            IMapRepository mapRepository,
            IUnitOfWork unitOfWork,
            IServerRepository serverRepository,
            MonsterSpawner spawner,
            TimeProvider timeProvider,
            ILogger<SpawnMonstersCommandHandler> logger)
        {
            _monsterRepository = monsterRepository;
            _mapRepository = mapRepository;
            _unitOfWork = unitOfWork;
            _serverRepository = serverRepository;
            _spawner = spawner;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SpawnMonstersCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var current = await _monsterRepository.CountAsync(request.ServerId, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(request.ServerId, cancellationToken);
            var target = _spawner.GetTargetPopulation(serverLevel);
            var missing = Math.Min(target - current, MaxSpawnsPerRun);

            if (missing <= 0)
                return;

            var spawned = 0;

            // Клітини цього прогону ще не в БД: IsOccupiedAsync їх не побачить,
            // і два монстри отримали б однакові координати → падіння на unique index
            var reserved = new HashSet<(int X, int Y)>();

            for (var i = 0; i < missing; i++)
            {
                var spot = await _spawner.TrySpawnAsync(
                    request.ServerId, serverLevel, 
                    async (x, y) => reserved.Contains((x, y))|| await _mapRepository.IsOccupiedAsync(request.ServerId, x, y, cancellationToken));

                if (spot is null)
                    break; // місця не знайшлося — спробуємо наступного разу

                var (type, level, x, y) = spot.Value;
                reserved.Add((x, y));

                var monster = new Monster(Guid.NewGuid(), request.ServerId, type, level, x, y, now);

                await _monsterRepository.AddAsync(monster, cancellationToken);
                await _mapRepository.AddAsync(
                    new MapCell(Guid.NewGuid(), request.ServerId, x, y, MapOccupantType.Monster, monster.Id),
                    cancellationToken);

                spawned++;
            }

            if (spawned > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Spawned {Spawned} monsters ({Current}/{Target})", spawned, current + spawned, target);
            }
        }
    }
}
