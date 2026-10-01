using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Payments.Commands
{
    /// <summary>Позначити платіж невдалим за Id сесії: відкладена оплата відхилена або сесія прострочена.</summary>
    public record FailPaymentCommand(string SessionId) : IRequest;

    /// <summary>
    /// Викликається лише з вебхука. Ідемпотентна: Stripe повторює подію, доки не отримає 200,
    /// а зарахований платіж невдалим не стає — gems уже в гаманці.
    /// </summary>
    public sealed class FailPaymentCommandHandler : IRequestHandler<FailPaymentCommand>
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FailPaymentCommandHandler> _logger;

        public FailPaymentCommandHandler(IPaymentRepository paymentRepository, IUnitOfWork unitOfWork,
            ILogger<FailPaymentCommandHandler> logger)
        {
            _paymentRepository = paymentRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task Handle(FailPaymentCommand request, CancellationToken cancellationToken)
        {
            var payment = await _paymentRepository.GetBySessionIdAsync(request.SessionId, cancellationToken);

            if (payment is null)
            {
                _logger.LogWarning("Failure webhook for unknown session {SessionId} ignored.", request.SessionId);
                return;
            }

            if (payment.Status != PaymentStatus.Pending)
            {
                _logger.LogInformation("Payment {PaymentId} is already {Status}, failure webhook ignored.", payment.Id, payment.Status);
                return;
            }

            payment.Fail();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Payment {PaymentId} failed for player {PlayerId}", payment.Id, payment.PlayerId);
        }
    }
}
