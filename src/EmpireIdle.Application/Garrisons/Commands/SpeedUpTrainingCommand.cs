using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>Миттєво завершити тренування за gems.</summary>
    public record SpeedUpTrainingCommand(Guid PlayerId, Guid OrderId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SpeedUpTrainingCommandHandler : IRequestHandler<SpeedUpTrainingCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;
        private readonly ILogger<SpeedUpTrainingCommandHandler> _logger;

        public SpeedUpTrainingCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator,
            ILogger<SpeedUpTrainingCommandHandler> logger)
        {
            _villageRepository = villageRepository;
            _garrisonRepository = garrisonRepository;
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(SpeedUpTrainingCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.GetByPlayerIdAsync(request.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Village not found for player {request.PlayerId}.");

            var garrison = await _garrisonRepository.GetByVillageIdAsync(village.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Garrison not found for village {village.Id}.");

            var order = garrison.TrainingOrders.FirstOrDefault(o => o.Id == request.OrderId)
                ?? throw new EntityNotFoundException($"Training order", request.OrderId);

            var cost = _calculator.GetInstantFinishCost(order.CompletesAt, now);

            if (cost > 0)
            {
                var userId = _currentPlayer.UserId
                    ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

                var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                    ?? throw new InvalidOperationException($"Wallet not found.");

                wallet.SpendGems(new GemAmount(cost), $"Speed up training of {order.UnitType}", request.PlayerId, now);
            }

            garrison.ReduceTrainingTime(order.Id, order.CompletesAt - now);
            garrison.CompleteDueTraining(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up training {OrderId} for {Cost} gems",
                request.PlayerId, request.OrderId, cost);
        }
    }
}
