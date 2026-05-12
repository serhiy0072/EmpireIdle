using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Services
{
    /// <summary>
    /// Сервіс для апгрейду будівлі в селі гравця.
    /// </summary>
    public class UpgradeBuildingService
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpgradeBuildingService> _logger;
        private readonly GameConfig _gameConfig;

        public UpgradeBuildingService(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<UpgradeBuildingService> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        /// <summary>
        /// Апгрейдити будівлю в селі гравця.
        /// </summary>
        public async Task UpgradeAsync(Guid playerId, Guid buildingId, CancellationToken cancellationToken = default)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(playerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {playerId}.");

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            village.UpdateBuilding(buildingId, buildingConfigs);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation($"Building {buildingId} upgraded in village {village.Id} for player {playerId}");
        }
    }
}
