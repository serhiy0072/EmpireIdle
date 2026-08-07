using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>
    /// Команда тренування партії юнітів (1–5) у казармах села гравця.
    /// </summary>
    public record TrainUnitsCommand(Guid PlayerId, string UnitType, int Count) : IRequest, IPlayerScopedRequest;

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
        private readonly GameConfig _gameConfig;

        public TrainUnitsCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            ILogger<TrainUnitsCommandHandler> logger,
            IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task Handle(TrainUnitsCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var config = _gameConfig.Units.FirstOrDefault(u => u.Key == request.UnitType)
                ?? throw new InvalidOperationException($"Unknown unit type '{request.UnitType}'.");

            village.ChargeCost(config.Cost, request.Count);

            garrison.TrainUnits(request.UnitType, request.Count,
                TimeSpan.FromMinutes(config.BaseTrainMinutes * request.Count), DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Training {Count} x {UnitType} started for village {VillageId} (player {PlayerId})",
                request.Count, request.UnitType, village.Id, request.PlayerId);
        }
    }
}
