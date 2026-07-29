
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Villages.Commands
{
    public record CompleteConstructionsCommand : IRequest;

    public class CompleteConstructionsCommandHandler : IRequestHandler<CompleteConstructionsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly ILogger<CompleteConstructionsCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public CompleteConstructionsCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, IGarrisonRepository garrisonRepository, ILogger<CompleteConstructionsCommandHandler> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
            _garrisonRepository = garrisonRepository;
            _logger = logger;
            _gameConfig = gameConfig.Value;
        }

        public async Task Handle(CompleteConstructionsCommand request, CancellationToken cancellationToken)
        {
            var villages = await _villageRepository.GetAllAsync(cancellationToken);
            var buildingConfigs = _gameConfig.Buildings.ToDictionary(b => b.Key, b => b);
            var now = DateTime.UtcNow;
            var completed = 0;

            foreach (var village in villages)
                completed += village.CompleteDueConstructions(now, buildingConfigs);

            var garrisons = await _garrisonRepository.GetAllAsync(cancellationToken);
            var trained = 0;

            foreach(var garrison in garrisons)
                trained += garrison.CompleteDueTraining(now);

            if (completed > 0 || trained > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Completed {Constructions} constructions, {Trainings} trainings", completed, trained);
            }
        }
    }
}
