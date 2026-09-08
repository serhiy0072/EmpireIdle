using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Resources;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Команда: зібрати накопичені ресурси з буфера будівлі.</summary>
    public record CollectBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CollectBuildingCommandHandler : IRequestHandler<CollectBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServerRepository _serverRepository;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly WorldGeometry _geometry;
        private readonly VillageCapacities _capacities;
        private readonly ILogger<CollectBuildingCommandHandler> _logger;

        public CollectBuildingCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            IServerRepository serverRepository,
            EffectResolver effectResolver,
            GameCatalog catalog,
            TimeProvider timeProvider,
            WorldGeometry geometry,
            VillageCapacities capacities,
            ILogger<CollectBuildingCommandHandler> logger)
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

        public async Task Handle(CollectBuildingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Кап залежить від того, що виробляє будівля, а команда цього
            // не знає — тож тип визначаємо до виклику
            var building = village.Buildings.FirstOrDefault(b => b.Id == request.BuildingId)
                ?? throw new EntityNotFoundException("Building", request.BuildingId);

            var storageCap = _catalog.Buildings.TryGetValue(building.Type, out var buildingConfig)
                && buildingConfig.ProducesResource is { } resourceKey
                ? _capacities.StorageCapFor(village, resourceKey)
                : 0;

            // Буфер рахується від останньої матеріалізації, тож потрібне вікно буста
            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);

            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);

            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            village.CollectFromBuilding(request.BuildingId, _catalog.Buildings, storageCap, now, boost, locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Building {BuildingId} collected for player {PlayerId}", request.BuildingId, request.PlayerId);
        }
    }
}
