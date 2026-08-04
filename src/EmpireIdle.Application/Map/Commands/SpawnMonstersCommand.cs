using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Map.Commands
{
    /// <summary>Доповнює популяцію монстрів до цільової щільності.</summary>
    public record SpawnMonstersCommand : IRequest;

    public class SpawnMonstersCommandHandler : IRequestHandler<SpawnMonstersCommand>
    {
        private const int ServerId = 1;          // мультисервер — post-MVP
        private const int MaxSpawnsPerRun = 50;  // не засівати всю карту одним прогоном

        private readonly IMonsterRepository _monsterRepository;
        private readonly IMapRepository _mapRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly MonsterSpawner _spawner;
        private readonly ILogger<SpawnMonstersCommandHandler> _logger;

        public SpawnMonstersCommandHandler(
            IMonsterRepository monsterRepository,
            IMapRepository mapRepository,
            IUnitOfWork unitOfWork,
            MonsterSpawner spawner,
            ILogger<SpawnMonstersCommandHandler> logger)
        {
            _monsterRepository = monsterRepository;
            _mapRepository = mapRepository;
            _unitOfWork = unitOfWork;
            _spawner = spawner;
            _logger = logger;
        }

        public async Task Handle(SpawnMonstersCommand request, CancellationToken cancellationToken)
        {
            var current = await _monsterRepository.CountAsync(ServerId, cancellationToken);
            var target = _spawner.GetTargetPopulation();
            var missing = Math.Min(target - current, MaxSpawnsPerRun);

            if (missing <= 0)
                return;

            var spawned = 0;

            for (var i = 0; i < missing; i++)
            {
                var spot = await _spawner.TrySpawnAsync(
                    ServerId,
                    (x, y) => _mapRepository.IsOccupiedAsync(ServerId, x, y, cancellationToken));

                if (spot is null)
                    break; // місця не знайшлося — спробуємо наступного разу

                var (type, level, x, y) = spot.Value;
                var monster = new Monster(Guid.NewGuid(), ServerId, type, level, x, y);

                await _monsterRepository.AddAsync(monster, cancellationToken);
                await _mapRepository.AddAsync(
                    new MapCell(Guid.NewGuid(), ServerId, x, y, MapOccupantType.Monster, monster.Id),
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