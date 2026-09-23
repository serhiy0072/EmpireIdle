using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Villages.ReadModels;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Команда: зібрати накопичені ресурси з усіх будівель села разом.</summary>
    public record CollectAllBuildingsCommand(Guid PlayerId) : IRequest<CollectAllView>, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CollectAllBuildingsCommandHandler : IRequestHandler<CollectAllBuildingsCommand, CollectAllView>
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

        public async Task<CollectAllView> Handle(CollectAllBuildingsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            var summary = village.CollectAll(_catalog.Buildings,
                resource => _capacities.StorageCapFor(village, resource), now, boost, locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Collected all buildings for player {PlayerId}; full storages: {FullStorages}",
                request.PlayerId, summary.FullStorages);

            return new CollectAllView(
                summary.Collected.Select(c => new CollectedResourceView(c.Key, c.Value)).ToList(),
                summary.FullStorages.ToList());
        }
    }
}
