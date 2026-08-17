using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Команда: зібрати накопичені ресурси з буфера будівлі.</summary>
    public record CollectBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public class CollectBuildingCommandHandler : IRequestHandler<CollectBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CollectBuildingCommand> _logger;
        private readonly GameCatalog _catalog;

        public CollectBuildingCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<CollectBuildingCommand> logger, GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _catalog = catalog;
        }

        public async Task Handle(CollectBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var buildingConfigs = _catalog.Buildings.ToDictionary(b => b.Key, b => b);

            village.CollectFromBuilding(request.BuildingId, _catalog.Buildings, DateTime.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Building {BuildingId} collected for player {PlayerId}", request.BuildingId, request.PlayerId);
        }
    }
}
