using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>
    /// Команда тренування партії юнітів (1–5) у казармах села гравця.
    /// </summary>
    public record TrainUnitsCommand(Guid PlayerId, string UnitType, int Count) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник TrainUnitsCommand: координує Village (списання вартості)
    /// та Garrison (постановка в чергу тренування) в одній транзакції.
    /// </summary>
    internal class TrainUnitsCommandHandler : IRequestHandler<TrainUnitsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TrainUnitsCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        public TrainUnitsCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            ILogger<TrainUnitsCommandHandler> logger,
            GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(TrainUnitsCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var config = _catalog.Unit(request.UnitType)
                ?? throw new InvalidOperationException($"Unknown unit type '{request.UnitType}'.");

            if (config.RequiresBuilding is not null && !village.HasBuilding(config.RequiresBuilding))
                throw new InvalidOperationException($"Training '{request.UnitType}' requires a '{config.RequiresBuilding}'.");

            village.ChargeCost(config.Cost, DateTime.UtcNow, request.Count);

            garrison.TrainUnits(request.UnitType, request.Count, _catalog.Config.MaxTrainingBatchSize,
                TimeSpan.FromMinutes(config.BaseTrainMinutes * request.Count), DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Training {Count} x {UnitType} started for village {VillageId} (player {PlayerId})",
                request.Count, request.UnitType, village.Id, request.PlayerId);
        }
    }
}
