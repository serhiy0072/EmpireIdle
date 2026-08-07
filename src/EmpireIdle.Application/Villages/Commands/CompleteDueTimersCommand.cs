
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Villages.Commands
{
    public record CompleteDueTimersCommand : IRequest;

    public class CompleteDueTimersCommandHandler : IRequestHandler<CompleteDueTimersCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly ILogger<CompleteDueTimersCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public CompleteDueTimersCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, IGarrisonRepository garrisonRepository, ILogger<CompleteDueTimersCommandHandler> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _garrisonRepository = garrisonRepository;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task Handle(CompleteDueTimersCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);

            var villages = await _villageRepository.GetWithDueConstructionsAsync(now, cancellationToken);
            var completed = 0;

            foreach (var village in villages)
                completed += village.CompleteDueConstructions(now, buildingConfigs);

            var garrisons = await _garrisonRepository.GetWithDueTrainingAsync(now, cancellationToken);
            var trained = 0;

            foreach (var garrison in garrisons)
                trained += garrison.CompleteDueTraining(now);

            if (completed > 0 || trained > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Completed {Constructions} constructions, {Trainings} trainings", completed, trained);
            }
        }
    }
}
