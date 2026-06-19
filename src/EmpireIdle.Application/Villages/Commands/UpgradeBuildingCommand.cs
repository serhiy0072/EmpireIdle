using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда апгрейду будівлі в селі гравця.
    /// </summary>
    public record UpgradeBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest;

    /// <summary>
    /// Обробник команди UpgradeBuildingCommand.
    /// </summary>
    public class UpgradeBuildingCommandHandler : IRequestHandler<UpgradeBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpgradeBuildingCommandHandler> _logger;
        private readonly GameConfig _gameConfig;
        private readonly IGameNotifier _notifier;

        public UpgradeBuildingCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<UpgradeBuildingCommandHandler> logger, IOptions<GameConfig> gameConfig, IGameNotifier notifier)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
            _notifier = notifier;
        }

        public async Task Handle(UpgradeBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            village.UpgradeBuilding(request.BuildingId, buildingConfigs);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var building = village.Buildings.First(b => b.Id == request.BuildingId);
            await _notifier.NotifyBuildingUpgradedAsync(
                request.PlayerId, request.BuildingId, building.Level.Value, cancellationToken);

            _logger.LogInformation(
                "Building {BuildingId} upgraded in village {VillageId} for player {PlayerId}",
                request.BuildingId, village.Id, request.PlayerId);
        }
    }
}