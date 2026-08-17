using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

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
        private readonly IActiveEffectRepository _effectRepository;
        private readonly ILogger<TickAllVillagesCommandHandler> _logger;
        private readonly GameCatalog _catalog;

        private const int BatchSize = 200;

        public TickAllVillagesCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<TickAllVillagesCommandHandler> logger, 
            GameCatalog catalog, IActiveEffectRepository effectRepository)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _catalog = catalog;
            _effectRepository = effectRepository;
        }

        public async Task Handle(TickAllVillagesCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var buildingConfigs = _catalog.Buildings.ToDictionary(b => b.Key, b => b);
            var total = 0;
            Guid? cursor = null;

            while (true)
            {
                var batch = await _villageRepository.GetBatchForTickAsync(cursor, BatchSize, cancellationToken);
                if (batch.Count == 0)
                    break;

                // Один запит на весь батч замість запиту на кожне село
                var multipliers = await _effectRepository.GetActiveMultipliersAsync(
                    batch.Select(v => v.PlayerId), EffectTarget.Production, now, cancellationToken);

                foreach (var village in batch)
                {
                    var multiplier = multipliers.GetValueOrDefault(village.PlayerId, 1.0);
                    village.TickProduction(_catalog.Buildings, now, multiplier);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                total += batch.Count;
                cursor = batch[^1].Id;

                if (batch.Count < BatchSize)
                    break;
            }

            _logger.LogInformation("Resource tick completed for {Count} villages", total);
        }
    }
}
