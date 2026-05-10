using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Infrastructure.Services
{
    /// <summary>
    /// Обробляє тік ресурсів для всіх сел.
    /// Викликається Hangfire recurring job кожну хвилину.
    /// </summary>
    public class ResourceTickService : IResourceTickService
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ResourceTickService> _logger;
        private readonly GameConfig _gameConfig;


        public ResourceTickService(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<ResourceTickService> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }
        public async Task TickAllVillagesAsync(CancellationToken cancellationToken = default)
        {
            var villages = await _villageRepository.GetAllAsync(cancellationToken);

            // Перетворюємо List<BuildingConfig> у Dictionary<string, BuildingConfig>
            // для швидкого доступу за ключем будівлі (наприклад "farm" → config)
            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            _logger.LogInformation("Resource tick started for {Count} villages", villages.Count);

            foreach(var village in villages)
            {
                village.CollectResources(buildingConfigs);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Resource tick completed for {Count} villages", villages.Count);
        }
    }
}
