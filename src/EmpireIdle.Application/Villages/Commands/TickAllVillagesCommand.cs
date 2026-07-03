using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда тіку ресурсів для всіх сел. Викликається Hangfire recurring job.
    /// </summary>
    public record TickAllVillagesCommand : IRequest;

    /// <summary>
    /// Обробник команди TickAllVillagesCommand.
    /// </summary>
    public class TickAllVillagesCommandHandler : IRequestHandler<TickAllVillagesCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<TickAllVillagesCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public TickAllVillagesCommandHandler(
            IVillageRepository villageRepository, 
            IUnitOfWork unitOfWork, 
            ILogger<TickAllVillagesCommandHandler> logger, 
            IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task Handle(TickAllVillagesCommand request, CancellationToken cancellationToken)
        {
            var villages = await _villageRepository.GetAllAsync(cancellationToken);

            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);
            
            _logger.LogInformation("Resource tick for {Count} villages", villages.Count);

            foreach(var village in villages)
            {
                village.TickProduction(buildingConfigs);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Resource tick completed for {Count} villages.", villages.Count);
        }
    }
}
