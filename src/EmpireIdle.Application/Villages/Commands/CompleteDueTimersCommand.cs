
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Villages.Commands
{
    public record CompleteDueTimersCommand : IRequest;

    public class CompleteDueTimersCommandHandler : IRequestHandler<CompleteDueTimersCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IActiveEffectRepository _effectRepository;
        private readonly ILogger<CompleteDueTimersCommandHandler> _logger;
        private readonly GameCatalog _catalog;
        private readonly TimeProvider _timeProvider;

        public CompleteDueTimersCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, IGarrisonRepository garrisonRepository, 
                IActiveEffectRepository effectRepository, ILogger<CompleteDueTimersCommandHandler> logger, GameCatalog catalog, TimeProvider timeProvider)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _garrisonRepository = garrisonRepository;
            _effectRepository = effectRepository;
            _timeProvider = timeProvider;
            _logger = logger;
            _catalog = catalog;

        }

        public async Task Handle(CompleteDueTimersCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var buildingConfigs = _catalog.Buildings.ToDictionary(b => b.Key, b => b);

            var villages = await _villageRepository.GetWithDueConstructionsAsync(now, cancellationToken);
            var completed = 0;

            foreach (var village in villages)
                completed += village.CompleteDueConstructions(now, _catalog.Buildings);

            var garrisons = await _garrisonRepository.GetWithDueTrainingAsync(now, _catalog.Config.ScanBatchSize, cancellationToken);
            var trained = 0;

            foreach (var garrison in garrisons)
                trained += garrison.CompleteDueTraining(now);

            if (completed > 0 || trained > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Completed {Constructions} constructions, {Trainings} trainings", completed, trained);
            }

            var removed = await _effectRepository.RemoveExpiredAsync(now, cancellationToken);
            if (removed > 0)
                _logger.LogInformation("Removed {Count} expired effects", removed);
        }
    }
}
