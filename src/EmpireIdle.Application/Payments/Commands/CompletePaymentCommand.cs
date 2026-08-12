using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Payments.Commands
{
    /// <summary>Підтвердити оплату за Id сесії й нарахувати gems.</summary>
    public record CompletePaymentCommand(string SessionId) : IRequest;

    /// <summary>
    /// Викликається лише з вебхука — не має IPlayerScopedRequest,
    /// бо власника визначає збережений Payment, а не токен.
    /// </summary>
    public class CompletePaymentCommandHandler : IRequestHandler<CompletePaymentCommand>
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IPlayerWalletRepository _walletRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CompletePaymentCommandHandler> _logger;

        public CompletePaymentCommandHandler(IPaymentRepository paymentRepository,IPlayerWalletRepository walletRepository,IPlayerRepository playerRepository,
            IUnitOfWork unitOfWork,ILogger<CompletePaymentCommandHandler> logger)
        {
            _paymentRepository = paymentRepository;
            _walletRepository = walletRepository;
            _playerRepository = playerRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Handle(CompletePaymentCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var payment = await _paymentRepository.GetBySessionIdAsync(request.SessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Payment for session '{request.SessionId}' not found.");

            // Stripe надсилає вебхук повторно, доки не отримає 200 — другий раз нічого не нараховуємо
            if (!payment.Complete(now))
            {
                _logger.LogInformation("Payment {PaymentId} already completed, webhook ignored.", payment.Id);
                return;
            }

            var player = await _playerRepository.GetByIdAsync(payment.PlayerId, cancellationToken)
                ?? throw new InvalidOperationException($"Player {payment.PlayerId} not found.");

            var wallet = await _walletRepository.GetByUserIdAsync(player.UserId, cancellationToken)
                ?? throw new InvalidOperationException($"Wallet not found for player {payment.PlayerId}.");

            wallet.AddGems(new GemAmount(payment.Gems), payment.SessionId, payment.PlayerId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Credited {Gems} gems to player {PlayerId} for payment {PaymentId}",
                payment.Gems, payment.PlayerId, payment.Id);
        }
    }
}
