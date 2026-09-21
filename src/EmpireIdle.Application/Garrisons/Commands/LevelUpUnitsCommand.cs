using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>
    /// Команда прокачки партії юнітів, які вже в гарнізоні, на вищий рівень.
    /// Партія більша за тренувальну, крок коротший (§5.2 GDD) — прокачка
    /// виграє пропускною здатністю, не знижкою.
    /// </summary>
    public record LevelUpUnitsCommand(Guid PlayerId, string UnitType, int FromLevel, int ToLevel, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    internal sealed class LevelUpUnitsCommandHandler : IRequestHandler<LevelUpUnitsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<LevelUpUnitsCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public LevelUpUnitsCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<LevelUpUnitsCommandHandler> logger,
            GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(LevelUpUnitsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var config = _catalog.FindUnit(request.UnitType)
                ?? throw new EntityNotFoundException("Unit type", request.UnitType);

            if (request.ToLevel > _catalog.Config.MaxUnitLevel)
                throw new RequirementNotMetException($"Unit level must be at most {_catalog.Config.MaxUnitLevel}.");

            if (config.RequiresBuilding is null)
                throw new InvalidOperationException($"Unit '{request.UnitType}' has no training building configured.");

            var trainingBuilding = village.Buildings
                .FirstOrDefault(b => b.Type == config.RequiresBuilding && !b.IsUnderConstruction)
                ?? throw new RequirementNotMetException(
                    $"Levelling up '{request.UnitType}' requires a '{config.RequiresBuilding}'.");

            // Сума кроків від fromLevel до toLevel — стрибок через рівні коштує
            // так само, як послідовна прокачка, не менше (§5.2 GDD)
            var costPerUnit = config.Cost
                .Select(line => new ResourceCost
                {
                    Resource = line.Resource,
                    Amount = ProgressionCurves.CumulativeUnitLevelCost(line.Amount, request.FromLevel, request.ToLevel, config.LevelUpCostGrowth)
                })
                .ToList();

            var minutesPerUnit = ProgressionCurves.CumulativeUnitLevelCost(
                config.BaseTrainMinutes, request.FromLevel, request.ToLevel, config.LevelUpCostGrowth);

            village.ChargeCost(costPerUnit, now, request.Count);

            garrison.LevelUpUnits(request.UnitType, request.FromLevel, request.ToLevel, request.Count,
                _catalog.Config.MaxLevelUpBatchSize, TimeSpan.FromMinutes(minutesPerUnit * request.Count), now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Levelling up {Count} x {UnitType} from {FromLevel} to {ToLevel} for village {VillageId} (player {PlayerId})",
                request.Count, request.UnitType, request.FromLevel, request.ToLevel, village.Id, request.PlayerId);
        }
    }
}
