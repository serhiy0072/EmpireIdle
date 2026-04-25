using EmpireIdle.Application.Interfaces;
using Microsoft.Extensions.Logging;

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


        public ResourceTickService(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<ResourceTickService> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task TickAllVillagesAsync(CancellationToken cancellationToken = default)
        {
            var villages = await _villageRepository.GetAllAsync(cancellationToken);

            _logger.LogInformation("Resource tick started for {Count} villages", villages.Count);

            foreach(var village in villages)
            {
                village.CollectResources();
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Resource tick completed for {Count} villages", villages.Count);
        }
    }
}
