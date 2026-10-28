using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Research.Commands
{
    /// <summary>Миттєво завершити поточне дослідження за gems — за тією ж ціною хвилини, що й будівництво.</summary>
    public record SpeedUpResearchCommand(Guid PlayerId) : IRequest, IPlayerScopedRequest, IIdempotentRequest;

    public sealed class SpeedUpResearchCommandHandler : IRequestHandler<SpeedUpResearchCommand>
    {
        private readonly IPlayerResearchRepository _research;
        private readonly IPlayerWalletRepository _wallets;
        private readonly ICurrentPlayer _currentPlayer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SpeedUpCalculator _calculator;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<SpeedUpResearchCommandHandler> _logger;

        public SpeedUpResearchCommandHandler(IPlayerResearchRepository research, IPlayerWalletRepository wallets,
            ICurrentPlayer currentPlayer, IUnitOfWork unitOfWork, SpeedUpCalculator calculator, TimeProvider timeProvider,
            ILogger<SpeedUpResearchCommandHandler> logger)
        {
            _research = research;
            _wallets = wallets;
            _currentPlayer = currentPlayer;
            _unitOfWork = unitOfWork;
            _calculator = calculator;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Handle(SpeedUpResearchCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var research = await _research.GetByPlayerAsync(request.PlayerId, cancellationToken);
            if (research?.CompletesAt is not { } completesAt)
                throw new RequirementNotMetException(RefusalReasons.ResearchNotActive, $"Player {request.PlayerId} is not researching.");

            var cut = _calculator.RequireCut(SpeedUpTimer.Research, completesAt, now);
            var cost = _calculator.GetCost(SpeedUpTimer.Research, completesAt, now);

            var userId = _currentPlayer.UserId
                ?? throw new UnauthorizedAccessException("This operation requires an authenticated account.");
            var wallet = await _wallets.GetByUserIdAsync(userId, cancellationToken)
                ?? throw new InvalidOperationException("Wallet not found.");

            wallet.SpendGems(new GemAmount(cost), $"Speed up research {research.ActiveKey}", request.PlayerId, now);

            research.Shorten(cut, now);

            // Дозріле завершуємо одразу: гравець заплатив за «зараз»
            research.CompleteDue(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Player {PlayerId} sped up research for {Cost} gems", request.PlayerId, cost);
        }
    }
}
