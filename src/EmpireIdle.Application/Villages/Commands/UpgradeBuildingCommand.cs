using EmpireIdle.Application.Common.Security;
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
    public record UpgradeBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest, IPlayerScopedRequest;

    /// <summary>
    /// Обробник команди UpgradeBuildingCommand.
    /// </summary>
    public class UpgradeBuildingCommandHandler : IRequestHandler<UpgradeBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpgradeBuildingCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public UpgradeBuildingCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<UpgradeBuildingCommandHandler> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task Handle(UpgradeBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            village.BeginBuildingUpgrade(request.BuildingId, buildingConfigs, DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Building {BuildingId} upgrade started in village {VillageId} for player {PlayerId}",
                request.BuildingId, village.Id, request.PlayerId);
        }
    }
}