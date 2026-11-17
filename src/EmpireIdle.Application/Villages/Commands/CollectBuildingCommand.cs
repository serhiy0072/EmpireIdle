using EmpireIdle.Application.Common;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    /// <summary>Команда: зібрати накопичені ресурси з буфера будівлі.</summary>
    public record CollectBuildingCommand(Guid PlayerId, Guid BuildingId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class CollectBuildingCommandHandler : IRequestHandler<CollectBuildingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly VillageProductionRates _rates;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CollectBuildingCommandHandler> _logger;

        public CollectBuildingCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            VillageProductionRates rates,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<CollectBuildingCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _rates = rates;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(CollectBuildingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.RequireByPlayerAsync(request.PlayerId, cancellationToken);

            // Буфер рахується від останньої матеріалізації, тож потрібне вікно буста
            var rates = await _rates.ForAsync(village, now, cancellationToken);

            village.CollectFromBuilding(request.BuildingId, _catalog.Buildings, now, rates.Boost, rates.LocationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Building {BuildingId} collected for player {PlayerId}", request.BuildingId, request.PlayerId);
        }
    }
}
