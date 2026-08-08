using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>Чим платити за лікування.</summary>
    public enum HealPaymentMethod
    {
        /// <summary>Ресурсами — половина вартості юніта.</summary>
        Resources = 1,

        /// <summary>Gems — фіксована ціна за юніта.</summary>
        Gems = 2
    }

    /// <summary>Вилікувати поранених: вони повертаються в гарнізон.</summary>
    public record HealWoundedCommand(Guid PlayerId, Dictionary<string, int> Units, HealPaymentMethod Payment)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public class HealWoundedCommandHandler : IRequestHandler<HealWoundedCommand>
    {
        /// <summary>Лікування ресурсами коштує половину вартості нового юніта.</summary>
        private const double HealCostFactor = 0.5;

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameConfig _gameConfig;
        private readonly ILogger<HealWoundedCommandHandler> _logger;

        public HealWoundedCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            IOptions<GameConfig> gameConfig,
            ILogger<HealWoundedCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _walletRepository = walletRepository;
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

            if (request.Payment == HealPaymentMethod.Gems)
                await ChargeGemsAsync(request, cancellationToken);
            else
                ChargeResources(request, village);

            var healed = garrison.HealWounded(request.Units);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Healed {Count} units for player {PlayerId} paying with {Payment}",
                healed.Values.Sum(), request.PlayerId, request.Payment);
        }

        /// <summary>Списує gems: фіксована ціна за кожного пораненого.</summary>
        private async Task ChargeGemsAsync(HealWoundedCommand request, CancellationToken cancellationToken)
        {
            var total = request.Units.Values.Where(c => c > 0).Sum();
            if (total == 0)
                throw new InvalidOperationException("Nothing to heal.");

            var cost = total * _gameConfig.Monetization.HealGemsPerUnit;

            var wallet = await _walletRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet not found for player {request.PlayerId}.");

            wallet.SpendGems(new GemAmount(cost), $"Heal {total} wounded units");
        }

        /// <summary>Списує ресурси: половина вартості створення юніта.</summary>
        private void ChargeResources(HealWoundedCommand request, Domain.Entities.Village village)
        {
            var cost = new List<ResourceCost>();

            foreach (var (unitType, count) in request.Units)
            {
                if (count <= 0)
                    continue;

                var config = _gameConfig.Units.FirstOrDefault(u => u.Key == unitType)
                    ?? throw new InvalidOperationException($"Unknown unit type '{unitType}'.");

                foreach (var line in config.Cost)
                {
                    // Населення не витрачається повторно — юніт живий, лише поранений
                    if (line.Resource == "population")
                        continue;

                    var amount = (int)Math.Ceiling(line.Amount * count * HealCostFactor);
                    var existing = cost.FirstOrDefault(c => c.Resource == line.Resource);

                    if (existing is null)
                        cost.Add(new ResourceCost { Resource = line.Resource, Amount = amount });
                    else
                        existing.Amount += amount;
                }
            }

            village.ChargeCost(cost);
        }
    }
}