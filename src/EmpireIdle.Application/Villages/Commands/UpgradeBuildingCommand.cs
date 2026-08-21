using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>
    /// Команда апгрейду будівлі в селі гравця.
    /// </summary>
    public record UpgradeBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник команди UpgradeBuildingCommand.
    /// </summary>
    public class UpgradeBuildingCommandHandler : IRequestHandler<UpgradeBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly EffectResolver _effectResolver;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<UpgradeBuildingCommandHandler> _logger;

        public UpgradeBuildingCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            EffectResolver effectResolver,
            TimeProvider timeProvider,
            GameCatalog catalog,
            ILogger<UpgradeBuildingCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _effectResolver = effectResolver;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(UpgradeBuildingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            // Апгрейд зупиняє виробництво — буфер треба зафіксувати за поточним множником
            var boost = await _effectResolver.GetProductionBoostAsync(request.PlayerId, now, cancellationToken);

            village.BeginBuildingUpgrade(request.BuildingId, _catalog.Buildings, now, boost);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Building {BuildingId} upgrade started in village {VillageId} for player {PlayerId}",
                request.BuildingId, village.Id, request.PlayerId);
        }
    }
}
