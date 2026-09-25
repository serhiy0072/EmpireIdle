using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Heroes.Commands
{
    /// <summary>Миттєво завершити прокачку героя за gems.</summary>
    public record SpeedUpHeroLevelUpCommand(Guid PlayerId, Guid OrderId)
        : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник SpeedUpHeroLevelUpCommand: списує gems, підтягує строк на
    /// «зараз» і одразу завершує замовлення тим самим шляхом, що й сканер —
    /// гравець бачить новий рівень у відповіді, а не через хвилину.
    /// </summary>
    public sealed class SpeedUpHeroLevelUpCommandHandler : IRequestHandler<SpeedUpHeroLevelUpCommand>
    {
        private readonly IHeroRepository _heroRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly SpeedUpCalculator _calculator;
        private readonly ILogger<SpeedUpHeroLevelUpCommandHandler> _logger;

        public SpeedUpHeroLevelUpCommandHandler(
            IHeroRepository heroRepository,
            IPlayerWalletRepository walletRepository,
            ICurrentPlayer currentPlayer,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            SpeedUpCalculator calculator,
            ILogger<SpeedUpHeroLevelUpCommandHandler> logger)
        {
            _heroRepository = heroRepository;
            _walletRepository = walletRepository;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _calculator = calculator;
            _logger = logger;
        }

        public async Task Handle(SpeedUpHeroLevelUpCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            // Через активне замовлення гравця, а не за id напряму — так чуже замовлення не прискорити
            var order = await _heroRepository.GetActiveOrderAsync(request.PlayerId, cancellationToken);

            if (order is null || order.Id != request.OrderId)
                throw new EntityNotFoundException("Hero level-up order", request.OrderId);

            // Останню хвилину прискорення не зрізає — прокачку завершить сканер
            var cut = _calculator.RequireCut(order.CompletesAt, now);
            var cost = _calculator.GetCost(order.CompletesAt, now);

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");

            var wallet = await _walletRepository.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            wallet.SpendGems(new GemAmount(cost), "Speed up hero level-up", request.PlayerId, now);

            order.Reduce(cut);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up hero level-up {OrderId} for {Cost} gems",
                request.PlayerId, request.OrderId, cost);
        }
    }
}
