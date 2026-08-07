using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>Вилікувати поранених: вони повертаються в гарнізон за половину вартості юніта.</summary>
    public record HealWoundedCommand(Guid PlayerId, Dictionary<string, int> Units) : IRequest, IPlayerScopedRequest;


    public class HealWoundedCommandHandler : IRequestHandler<HealWoundedCommand>
    {
        /// <summary>Лікування коштує половину вартості створення нового юніта.</summary>
        private const double HealCostFactor = 0.5;

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameConfig _gameConfig;
        private readonly ILogger<HealWoundedCommandHandler> _logger;

        public HealWoundedCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IUnitOfWork unitOfWork,
            IOptions<GameConfig> gameConfig,
            ILogger<HealWoundedCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _unitOfWork = unitOfWork;
            _gameConfig = gameConfig.Value;
            _logger = logger;
        }

        public async Task Handle(HealWoundedCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Рахуємо сумарну вартість лікування за всіма типами
            var cost = new List<ResourceCost>();

            foreach(var (unitType, count) in request.Units)
            {
                if (count < 0)
                    continue;

                var config = _gameConfig.Units.FirstOrDefault(u => u.Key == unitType)
                    ?? throw new InvalidOperationException($"Unknown unit type '{unitType}'.");

                foreach(var line in config.Cost)
                {
                    // Населення не витрачається повторно — юніт уже існує, він лише поранений
                    if (line.Resource == "population")
                        continue;

                    var amount = (int)Math.Ceiling(line.Amount * count * HealCostFactor);
                    var existing = cost.FirstOrDefault(c => c.Resource == line.Resource);

                    if(existing is null)
                        cost.Add(new ResourceCost { Resource = line.Resource, Amount = amount });
                    else
                        existing.Amount += amount;
                }
            }

            village.ChargeCost(cost);
            var healed = garrison.HealWounded(request.Units);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Healed {Count} units for player {PlayerId}", healed.Values.Sum(), request.PlayerId);
        }
    }
}
