using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
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

    public sealed class HealWoundedCommandHandler : IRequestHandler<HealWoundedCommand>
    {
        /// <summary>Лікування ресурсами коштує половину вартості нового юніта.</summary>
        private const double HealCostFactor = 0.5;

        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly GameCatalog _catalog;
        private readonly ILogger<HealWoundedCommandHandler> _logger;

        public HealWoundedCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            GameCatalog catalog,
            ILogger<HealWoundedCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(HealWoundedCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var healed = garrison.HealWounded(request.Units, now);
            if (healed.Count == 0)
                throw new InvalidStateException("Nothing to heal.");

            if (request.Payment == HealPaymentMethod.Gems)
                await ChargeGemsAsync(healed, request.PlayerId, now, cancellationToken);
            else
                ChargeResources(healed, village, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Healed {Count} units for player {PlayerId} paying with {Payment}",
                healed.Values.Sum(), request.PlayerId, request.Payment);
        }

        /// <summary>Списує gems: фіксована ціна за кожного вилікуваного.</summary>
        private async Task ChargeGemsAsync(IReadOnlyDictionary<string, int> healed, Guid playerId, DateTime utcNow, CancellationToken cancellationToken)
        {
            var total = healed.Values.Where(c => c > 0).Sum();
            var cost = total * _catalog.Config.Monetization.HealGemsPerUnit;

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            wallet.SpendGems(new GemAmount(cost), $"Heal {total} wounded units", playerId, utcNow);
        }

        /// <summary>Списує ресурси: половина вартості створення юніта.</summary>
        private void ChargeResources(IReadOnlyDictionary<string, int> healed, Domain.Entities.Village village, DateTime utcNow)
        {
            var cost = new List<ResourceCost>();

            foreach (var (unitType, count) in healed)
            {
                if (count <= 0)
                    continue;

                var config = _catalog.Unit(unitType)
                    ?? throw new EntityNotFoundException($"Unit type", unitType);

                foreach (var line in config.Cost)
                {
                    var amount = (int)Math.Ceiling(line.Amount * count * HealCostFactor);
                    var existing = cost.FirstOrDefault(c => c.Resource == line.Resource);

                    if (existing is null)
                        cost.Add(new ResourceCost { Resource = line.Resource, Amount = amount });
                    else
                        existing.Amount += amount;
                }
            }

            village.ChargeCost(cost, utcNow);
        }
    }
}
