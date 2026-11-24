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
    /// <summary>
    /// Миттєво відремонтувати всі пошкоджені будівлі за ресурси (GDD §2.6).
    /// Лише всі разом: серію поразок обнуляє тільки повне відновлення.
    /// </summary>
    public record RepairVillageCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RepairVillageCommandHandler : IRequestHandler<RepairVillageCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly VillageProductionRates _rates;
        private readonly GameCatalog _catalog;
        private readonly CityFallRules _rules;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<RepairVillageCommandHandler> _logger;

        public RepairVillageCommandHandler(
            IVillageRepository villageRepository,
            IUnitOfWork unitOfWork,
            VillageProductionRates rates,
            GameCatalog catalog,
            CityFallRules rules,
            TimeProvider timeProvider,
            ILogger<RepairVillageCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _rates = rates;
            _catalog = catalog;
            _rules = rules;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(RepairVillageCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.RequireByPlayerAsync(request.PlayerId, cancellationToken);

            // Ремонт матеріалізує буфери: вироблене до нього — за пошкодженим темпом
            var rates = await _rates.ForAsync(village, now, cancellationToken);

            village.RepairAll(_catalog.Buildings, now, _rules.RepairCostShare, rates.Boost, rates.LocationMultiplier);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Village {VillageId} repaired for resources.", village.Id);
        }
    }
}
