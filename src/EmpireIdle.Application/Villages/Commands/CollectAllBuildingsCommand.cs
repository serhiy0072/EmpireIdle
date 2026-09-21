using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Команда: зібрати накопичені ресурси з усіх будівель села разом.</summary>
    public record CollectAllBuildingsCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CollectAllBuildingsCommandHandler : IRequestHandler<CollectAllBuildingsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerRepository _serverRepository;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly WorldGeometry _geometry;
        private readonly VillageCapacities _capacities;
        private readonly ILogger<CollectAllBuildingsCommandHandler> _logger;

        public CollectAllBuildingsCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            IServerRepository serverRepository,
            EffectResolver effectResolver,
            GameCatalog catalog,
            TimeProvider timeProvider,
            WorldGeometry geometry,
            VillageCapacities capacities,
            ILogger<CollectAllBuildingsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _serverRepository = serverRepository;
            _effectResolver = effectResolver;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _geometry = geometry;
            _capacities = capacities;
            _logger = logger;
        }

        public async Task Handle(CollectAllBuildingsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            // Список ідентифікаторів знімаємо наперед: Collect змінює буфер,
            // а перебирати колекцію, яку сам же й міняєш, — джерело багів
            var collectableIds = village.Buildings
                .Where(b => !b.IsUnderConstruction
                    && _catalog.Buildings.TryGetValue(b.Type, out var config)
                    && config.ProducesResource is not null)
                .Select(b => b.Id)
                .ToList();

            foreach (var buildingId in collectableIds)
            {
                var building = village.Buildings.First(b => b.Id == buildingId);
                var config = _catalog.Buildings[building.Type];
                var storageCap = _capacities.StorageCapFor(village, config.ProducesResource!);

                village.CollectFromBuilding(buildingId, _catalog.Buildings, storageCap, now, boost, locationMultiplier);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Collected all buildings for player {PlayerId}", request.PlayerId);
        }
    }
}
