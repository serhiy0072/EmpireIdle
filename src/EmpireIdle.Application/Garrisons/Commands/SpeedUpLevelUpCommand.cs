using EmpireIdle.Application.Common;
using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Garrisons.Commands
{
    /// <summary>Миттєво завершити прокачку партії юнітів за gems.</summary>
    public record SpeedUpLevelUpCommand(Guid PlayerId, Guid OrderId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SpeedUpLevelUpCommandHandler : IRequestHandler<SpeedUpLevelUpCommand>
    {
        private readonly IVillageRepository _villageRepository;
        private readonly IGarrisonRepository _garrisonRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;
        private readonly ILogger<SpeedUpLevelUpCommandHandler> _logger;

        public SpeedUpLevelUpCommandHandler(
            IVillageRepository villageRepository,
            IGarrisonRepository garrisonRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator,
            ILogger<SpeedUpLevelUpCommandHandler> logger)
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

        public async Task Handle(SpeedUpLevelUpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var village = await _villageRepository.RequireByPlayerAsync(request.PlayerId, cancellationToken);

            var garrison = await _garrisonRepository.RequireByVillageAsync(village.Id, cancellationToken);

            var order = garrison.LevelUpOrders.FirstOrDefault(o => o.Id == request.OrderId)
                ?? throw new EntityNotFoundException("Level-up order", request.OrderId);

            // Межа прокачки нульова (рішення 08.10.2026): прискорення доводить її до кінця
            var cut = _calculator.RequireCut(SpeedUpTimer.UnitLevelUp, order.CompletesAt, now);
            var cost = _calculator.GetCost(SpeedUpTimer.UnitLevelUp, order.CompletesAt, now);

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.RequireByUserAsync(userId, cancellationToken);

            wallet.SpendGems(new GemAmount(cost), $"speedup:levelup:{order.UnitType}", request.PlayerId, now);

            garrison.ReduceLevelUpTime(order.Id, cut, now);

            // Дозріле завершуємо одразу, а не чекаємо сканера: гравець заплатив за «зараз»
            garrison.CompleteDueLevelUps(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up level-up {OrderId} for {Cost} gems",
                request.PlayerId, request.OrderId, cost);
        }
    }
}
