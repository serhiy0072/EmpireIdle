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
    /// <summary>Викупити відновлюваних юнітів за gems до спливання дедлайну.</summary>
    public record RecoverUnitsCommand(Guid PlayerId, Dictionary<string, int> Units)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class RecoverUnitsCommandHandler : IRequestHandler<RecoverUnitsCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<RecoverUnitsCommandHandler> _logger;

        public RecoverUnitsCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            GameCatalog catalog,
            ILogger<RecoverUnitsCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task Handle(RecoverUnitsCommand request, CancellationToken cancellationToken)
        {
            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            // Спершу забираємо зі стеків — платимо рівно за те, що реально повернулось,
            // бо частина могла згоріти по дедлайну між показом екрана і натисканням кнопки
            var recovered = garrison.RecoverUnits(request.Units, DateTime.UtcNow);
            if (recovered.Count == 0)
                throw new InvalidStateException("Nothing to recover.");

            var cost = CalculateCost(recovered);

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            // Кине виняток при нестачі gems — SaveChanges не дійде, стеки лишаться на місці
            wallet.SpendGems(new GemAmount(cost), $"Recover {recovered.Values.Sum()} units", request.PlayerId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Recovered {Count} units for player {PlayerId} for {Cost} gems",
                recovered.Values.Sum(), request.PlayerId, cost);
        }

        /// <summary>Ціна викупу: сума RecoverCostGems по типах юнітів.</summary>
        private int CalculateCost(IReadOnlyDictionary<string, int> recovered)
        {
            var total = 0;

            foreach (var (unitType, count) in recovered)
            {
                var config = _catalog.Unit(unitType)
                    ?? throw new EntityNotFoundException($"Unit type", unitType);

                total += config.RecoverCostGems * count;
            }

            return total;
        }
    }
}
