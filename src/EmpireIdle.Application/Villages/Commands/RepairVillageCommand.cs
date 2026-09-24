using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Миттєво відремонтувати всі пошкоджені будівлі за ресурси (GDD §2.6).
    /// Лише всі разом: серію поразок обнуляє тільки повне відновлення.
    /// </summary>
    public record RepairVillageCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RepairVillageCommandHandler : IRequestHandler<RepairVillageCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IServerRepository _serverRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly WorldGeometry _geometry;
        private readonly CityFallRules _rules;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RepairVillageCommandHandler> _logger;

        public RepairVillageCommandHandler(
            IVillageRepository villageRepository,
            IServerRepository serverRepository,
            IUnitOfWork unitOfWork,
            EffectResolver effectResolver,
            GameCatalog catalog,
            WorldGeometry geometry,
            CityFallRules rules,
            TimeProvider timeProvider,
            ILogger<RepairVillageCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _serverRepository = serverRepository;
            _unitOfWork = unitOfWork;
            _effectResolver = effectResolver;
            _catalog = catalog;
            _geometry = geometry;
            _rules = rules;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RepairVillageCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new EntityNotFoundException("Village for player", request.PlayerId);

            // Ремонт матеріалізує буфери: вироблене до нього — за пошкодженим темпом
            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);
            var serverLevel = await _serverRepository.GetLevelAsync(village.ServerId, cancellationToken);
            var locationMultiplier = _geometry.ProductionMultiplierAt(village.X, village.Y, serverLevel);

            village.RepairAll(_catalog.Buildings, now, _rules.RepairCostShare, boost, locationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Village {VillageId} repaired for resources.", village.Id);
        }
    }
}
