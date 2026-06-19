using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда побудови нової будівлі в селі гравця.
    /// </summary>
    public record AddBuildingCommand(Guid PlayerId, string BuildingType) : IRequest<Guid>;

    /// <summary>
    /// Обробник команди AddBuildingCommand. Повертає Id створеної будівлі.
    /// </summary>
    public class AddBuildingCommandHandler : IRequestHandler<AddBuildingCommand, Guid>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AddBuildingCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public AddBuildingCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<AddBuildingCommandHandler> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task<Guid> Handle(AddBuildingCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var config = _gameConfig.Buildings.FirstOrDefault(b => b.Key == request.BuildingType)
                ?? throw new InvalidOperationException($"Unknown building type '{request.BuildingType}'.");

            // Перша будівля кожного типу безкоштовна, наступні коштують BaseCost
            var existingCount = village.Buildings.Count(b => b.Type == request.BuildingType);
            if (existingCount > 0)
            {
                var cost = config.BaseCost;
                var resource = village.Resources.FirstOrDefault(r => r.ResourceType == config.CostResource)
                    ?? throw new InvalidOperationException($"Resource '{config.CostResource}' not found in village.");

                if (resource.Amount < cost)
                    throw new InvalidOperationException(
                        $"Not enough {config.CostResource}: need {cost}, have {resource.Amount}.");

                resource.Amount -= cost;
            }

            var buildingId = Guid.NewGuid();
            var building = new Building(buildingId, village.Id, request.BuildingType);
            village.AddBuilding(building);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Building {BuildingType} ({BuildingId}) added to village {VillageId} for player {PlayerId}",
                request.BuildingType, buildingId, village.Id, request.PlayerId);

            return buildingId;
        }
    }
}