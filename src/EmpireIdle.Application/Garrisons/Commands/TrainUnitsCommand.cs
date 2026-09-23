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
    /// Команда тренування партії юнітів (1–5) у казармах села гравця.
    /// </summary>
    /// <param name="Level">
    /// Рівень, на якому юніти з'являться — з нуля, а не завжди 1. Ціна й час
    /// сумують крок за кроком від рівня 1 (§5.2 GDD): дорожче за пряме
    /// тренування, ніж прокачка потім, інакше прокачка втрачає сенс.
    /// </param>
    public record TrainUnitsCommand(Guid PlayerId, string UnitType, int Level, int Count)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник TrainUnitsCommand: координує Village (списання вартості)
    /// та Garrison (постановка в чергу тренування) в одній транзакції.
    /// </summary>
    internal sealed class TrainUnitsCommandHandler : IRequestHandler<TrainUnitsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TrainUnitsCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public TrainUnitsCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<TrainUnitsCommandHandler> logger,
            GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(TrainUnitsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var config = _catalog.FindUnit(request.UnitType)
                ?? throw new EntityNotFoundException($"Unit type", request.UnitType);

            if (config.RequiresBuilding is null)
                throw new InvalidOperationException($"Unit '{request.UnitType}' has no training building configured.");

            // Рівень будівлі визначає ліміт армії, тому потрібна сама будівля, а не факт її наявності.
            // Та, що в процесі будівництва, не рахується — інакше замовлення можна зробити наперед.
            var trainingBuilding = village.Buildings
                .FirstOrDefault(b => b.Type == config.RequiresBuilding && !b.IsUnderConstruction)
                ?? throw new RequirementNotMetException(RefusalReasons.BuildingRequired,
                    $"Training '{request.UnitType}' requires a '{config.RequiresBuilding}'.",
                    _catalog.Building(config.RequiresBuilding).DisplayName);

            if (trainingBuilding.Level.Value < config.RequiresBuildingLevel)
                throw new RequirementNotMetException(RefusalReasons.BuildingLevelRequired,
                    $"Training '{request.UnitType}' requires '{config.RequiresBuilding}' at level {config.RequiresBuildingLevel}.",
                    _catalog.Building(config.RequiresBuilding).DisplayName, config.RequiresBuildingLevel);

            if (request.Level < 1 || request.Level > _catalog.Config.MaxUnitLevel)
                throw new RequirementNotMetException($"Unit level must be between 1 and {_catalog.Config.MaxUnitLevel}.");

            var armyCapacity = trainingBuilding.Level.Value * _catalog.Config.ArmyCapacityPerBarracksLevel;

            // Тренування з нуля на обраний рівень коштує суму кроків від 1 до нього —
            // так само, як прокачка вже навченого юніта (§5.2 GDD)
            var costPerUnit = config.Cost
                .Select(line => new ResourceCost
                {
                    Resource = line.Resource,
                    Amount = ProgressionCurves.CumulativeUnitLevelCost(line.Amount, 1, request.Level + 1, config.LevelUpCostGrowth)
                })
                .ToList();

            var minutesPerUnit = ProgressionCurves.CumulativeUnitLevelCost(
                config.BaseTrainMinutes, 1, request.Level + 1, config.LevelUpCostGrowth);

            village.ChargeCost(costPerUnit, now, request.Count);

            garrison.TrainUnits(request.UnitType, request.Level, request.Count, _catalog.Config.MaxTrainingBatchSize,
                armyCapacity, TimeSpan.FromMinutes(minutesPerUnit * request.Count), now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Training {Count} x {UnitType} started for village {VillageId} (player {PlayerId}), capacity {Used}/{Capacity}",
                request.Count, request.UnitType, village.Id, request.PlayerId,
                garrison.Units.Sum(u => u.Count), armyCapacity);
        }
    }
}
