using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Services
{
    /// <summary>
    /// Побудувати нову будівлю в селі гравця.
    /// </summary>
    public class AddBuildingService
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AddBuildingService> _logger;
        private readonly GameConfig _gameConfig;

        public AddBuildingService(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<AddBuildingService> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        /// <summary>
        /// Побудувати нову будівлю вказаного типу в селі гравця.
        /// </summary>
        /// <returns>Id створеної будівлі.</returns>
        public async Task<Guid> AddAsync(Guid playerId, string buildingType, CancellationToken cancellationToken = default)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

            var config = _gameConfig.Buildings.FirstOrDefault(b => b.Key == buildingType)
                ?? throw new InvalidOperationException($"Unknown building type '{buildingType}'.");

            var existingCount = village.Buildings.Count(b => b.Type == buildingType);
            if(existingCount > 0)
            {
                var cost = config.BaseCost;
                var resource = village.Resources.FirstOrDefault(r => r.ResourceType == config.CostResource)
                    ?? throw new InvalidOperationException($"Resource '{config.CostResource}' not found in village.");

                if(resource.Amount< cost)
                    throw new InvalidOperationException($"Not enough {config.CostResource}: need {cost}, have {resource.Amount}.");

                resource.Amount -= cost;
            }

            var buildingId = Guid.NewGuid();
            var building = new Building(buildingId, village.Id, buildingType);
            village.AddBuilding(building);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Building {BuildingType} ({BuildingId}) added to village {VillageId} for player {PlayerId}", buildingType, buildingId, village.Id, playerId);

            return buildingId;
        }

    }
}
