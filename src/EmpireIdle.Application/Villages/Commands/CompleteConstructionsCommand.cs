
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
        private readonly ILogger<CompleteConstructionsCommandHandler> _logger;
        private readonly GameConfig _gameConfig;

        public CompleteConstructionsCommandHandler(IVillageRepository villageRepository, IUnitOfWork unitOfWork, ILogger<CompleteConstructionsCommandHandler> logger, IOptions<GameConfig> gameConfig)
        {
            _villageRepository = villageRepository;
            _unitOfWork = unitOfWork;
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

            if (completed > 0)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Completed {count} constructions", completed);
            }
        }
    }
}
