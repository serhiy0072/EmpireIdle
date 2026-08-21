using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда побудови нової будівлі в селі гравця.
    /// </summary>
    public record AddBuildingCommand(Guid PlayerId, string BuildingType) : IRequest<Guid>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник команди AddBuildingCommand. Повертає Id створеної будівлі.
    /// </summary>
    public class AddBuildingCommandHandler : IRequestHandler<AddBuildingCommand, Guid>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AddBuildingCommandHandler> _logger;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public AddBuildingCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<AddBuildingCommandHandler> logger,
            TimeProvider timeProvider, GameCatalog catalog)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _timeProvider = timeProvider;
            _catalog = catalog;
        }

        public async Task<Guid> Handle(AddBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var buildingConfigs = _catalog.Buildings.ToDictionary(b => b.Key, b => b);

            var buildingId = village.AddBuilding(request.BuildingType, _catalog.Buildings, _timeProvider.GetUtcNow().UtcDateTime);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Building {BuildingType} ({BuildingId}) added to village {VillageId} for player {PlayerId}",
                request.BuildingType, buildingId, village.Id, request.PlayerId);

            return buildingId;
        }
    }
}
