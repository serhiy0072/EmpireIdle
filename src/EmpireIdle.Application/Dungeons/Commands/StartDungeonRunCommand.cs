using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dungeons.Commands
{
    /// <summary>
    /// Починає забіг: списує енергію й ставить першу хвилю.
    ///
    /// Енергія платиться наперед, а не за результат: інакше програні спроби
    /// були б безкоштовними, і вибір рівня перестав би бути ризиком.
    /// </summary>
    public record StartDungeonRunCommand(Guid PlayerId, string DungeonKey, int Level, IReadOnlyList<Guid> HeroIds)
        : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class StartDungeonRunCommandHandler : IRequestHandler<StartDungeonRunCommand, Guid>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly IVillageRepository _villages;
        private readonly DungeonTeamFactory _teamFactory;
        private readonly BattleBuilder _builder;
        private readonly GameCatalog _catalog;
        private readonly IRandomSource _random;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StartDungeonRunCommandHandler> _logger;

        public StartDungeonRunCommandHandler(
            IDungeonRepository dungeons,
            IVillageRepository villages,
            DungeonTeamFactory teamFactory,
            BattleBuilder builder,
            GameCatalog catalog,
            IRandomSource random,
            IServerContext serverContext,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<StartDungeonRunCommandHandler> logger)
        {
            _dungeons = dungeons;
            _villages = villages;
            _teamFactory = teamFactory;
            _builder = builder;
            _catalog = catalog;
            _random = random;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<Guid> Handle(StartDungeonRunCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var settings = _catalog.Config.Dungeons;

            var dungeon = _catalog.Dungeons.GetValueOrDefault(request.DungeonKey)
                ?? throw new EntityNotFoundException("Dungeon", request.DungeonKey);

            if (request.Level < 1 || request.Level > settings.MaxLevel)
                throw new RequirementNotMetException($"Dungeon level must be between 1 and {settings.MaxLevel}.");

            if (request.HeroIds.Count == 0 || request.HeroIds.Count > settings.TeamSize)
                throw new RequirementNotMetException($"A dungeon team holds from one to {settings.TeamSize} heroes.");

            if (request.HeroIds.Distinct().Count() != request.HeroIds.Count)
                throw new RequirementNotMetException("The same hero cannot take two places in the team.");

            // Один забіг на гравця: інакше друга вкладка розпочала б паралельний бій
            if (await _dungeons.GetActiveRunAsync(request.PlayerId, cancellationToken) is not null)
                throw new AlreadyExistsException(RefusalReasons.DungeonRunInProgress, "Dungeon run", request.PlayerId.ToString());

            var village = await _villages.GetByPlayerIdReadOnlyAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", request.PlayerId);

            var mainLevel = village.Buildings
                .FirstOrDefault(b => b.Type == _catalog.MainBuildingKey)?.Level.Value ?? 0;

            if (mainLevel < dungeon.RequiresMainBuildingLevel)
                throw new RequirementNotMetException(RefusalReasons.DungeonTownHallRequired,
                    $"Dungeon '{dungeon.Key}' opens at town hall {dungeon.RequiresMainBuildingLevel}.",
                    dungeon.DisplayName, dungeon.RequiresMainBuildingLevel);

            var cleared = await _dungeons.GetClearedLevelsAsync(request.PlayerId, cancellationToken);
            var clearedLevel = cleared.GetValueOrDefault(dungeon.Key);

            // Рівень відкривається попереднім: перескочити третій рівень не можна
            if (request.Level > clearedLevel + 1)
                throw new RequirementNotMetException(RefusalReasons.DungeonLevelLocked,
                    $"Dungeon '{dungeon.Key}' level {request.Level} opens after clearing level {request.Level - 1}.",
                    request.Level, request.Level - 1);

            var energy = await _dungeons.GetEnergyAsync(request.PlayerId, cancellationToken);

            if (energy is null)
            {
                // Перший вхід — повна шкала: інакше новачок чекав би двадцять годин
                energy = new DungeonEnergy(Guid.NewGuid(), request.PlayerId, settings.MaxEnergy, now);
                await _dungeons.AddEnergyAsync(energy, cancellationToken);
            }

            energy.Spend(settings.EnergyPerRun, settings.MaxEnergy, settings.RegenHours, now);

            var team = await _teamFactory.BuildAsync(request.PlayerId, request.HeroIds, cancellationToken);
            var seed = _random.Next(int.MaxValue);
            var battle = _builder.Start(team, dungeon, request.Level, seed);

            var run = new DungeonRun(Guid.NewGuid(), request.PlayerId, _serverContext.ServerId,
                dungeon.Key, request.Level, BattleSerializer.Write(battle), now);

            await _dungeons.AddRunAsync(run, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} entered dungeon {DungeonKey} level {Level} with {Heroes} heroes",
                request.PlayerId, dungeon.Key, request.Level, team.Count);

            return run.Id;
        }
    }
}
