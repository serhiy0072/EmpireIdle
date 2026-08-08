using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
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
        private readonly EffectResolver _effectResolver;

        private const int BatchSize = 200;

        public TickAllVillagesCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<TickAllVillagesCommandHandler> logger, 
            IOptions<GameConfig> gameConfig, EffectResolver effectResolver)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _gameConfig = gameConfig.Value;
            _effectResolver = effectResolver;
        }

        public async Task Handle(TickAllVillagesCommand request, CancellationToken cancellationToken)
        {
            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);
            var total = 0;
            Guid? cursor = null;

            var now = DateTime.UtcNow;

            while (true)
            {
                var batch = await _villageRepository.GetBatchForTickAsync(cursor, BatchSize, cancellationToken);
                if (batch.Count == 0)
                    break;

                foreach (var village in batch)
                {
                    var multiplier = await _effectResolver.GetMultiplierAsync(
                        village.PlayerId, EffectTarget.Production, now, cancellationToken);

                    village.TickProduction(buildingConfigs, now, multiplier);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                total += batch.Count;
                cursor = batch[^1].Id;

                if (batch.Count < BatchSize)
                    break; // остання порція
            }

            _logger.LogInformation("Resource tick completed for {Count} villages", total);
        }
    }
}
