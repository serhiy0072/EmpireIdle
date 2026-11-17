using EmpireIdle.Application.Common;
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
        private readonly VillageProductionRates _rates;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CollectAllBuildingsCommandHandler> _logger;

        public CollectAllBuildingsCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            VillageProductionRates rates,
            GameCatalog catalog,
            TimeProvider timeProvider,
            ILogger<CollectAllBuildingsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _rates = rates;
            _catalog = catalog;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<CollectAllView> Handle(CollectAllBuildingsCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.RequireByPlayerAsync(request.PlayerId, cancellationToken);

            var rates = await _rates.ForAsync(village, now, cancellationToken);

            var summary = village.CollectAll(_catalog.Buildings, now, rates.Boost, rates.LocationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Collected all buildings for player {PlayerId}", request.PlayerId);

            return new CollectAllView(summary.Collected.Select(c => new CollectedResourceView(c.Key, c.Value)).ToList());
        }
    }
}
