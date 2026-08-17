using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Application.Payments.Commands
{
    /// <summary>Створити сесію оплати пакета gems.</summary>
    public record CreateCheckoutSessionCommand(Guid PlayerId, string PackKey)
        : IRequest<string>, IPlayerScopedRequest, IIdempotentRequest;

    /// <summary>
    /// Обробник: фіксує Pending-платіж і повертає посилання на Stripe Checkout.
    /// Gems нараховуються не тут, а вебхуком — повернення гравця на сайт нічого не доводить.
    /// </summary>
    public class CreateCheckoutSessionCommandHandler : IRequestHandler<CreateCheckoutSessionCommand, string>
    {
        private readonly IPaymentProvider _paymentProvider;
        private readonly IPaymentRepository _paymentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly GameCatalog _catalog;
        private readonly ILogger<CreateCheckoutSessionCommandHandler> _logger;

        public CreateCheckoutSessionCommandHandler(IPaymentProvider paymentProvider, IPaymentRepository paymentRepository, IUnitOfWork unitOfWork, 
                GameCatalog catalog, ILogger<CreateCheckoutSessionCommandHandler> logger)
        {
            _paymentProvider = paymentProvider;
            _paymentRepository = paymentRepository;
            _unitOfWork = unitOfWork;
            _catalog = catalog;
            _logger = logger;
        }

        public async Task<string> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            var pack = _catalog.Config.Shop.GemPacks.FirstOrDefault(p => p.Key == request.PackKey)
                ?? throw new InvalidOperationException($"Gem pack '{request.PackKey}' not found.");

            var session = await _paymentProvider.CreateSessionAsync(pack.Key, pack.DisplayName, pack.PriceCents, _catalog.Config.Shop.Currency, request.PlayerId, cancellationToken);

            // Ціну й кількість gems фіксуємо тут: конфіг може змінитись до оплати
            var payment = new Payment(Guid.NewGuid(), request.PlayerId, pack.Key, pack.Gems, pack.PriceCents, _catalog.Config.Shop.Currency, session.SessionId, now);

            await _paymentRepository.AddAsync(payment, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Checkout session {SessionId} created for player {PlayerId}, pack {PackKey}", session.SessionId, request.PlayerId, pack.Key);

            return session.CheckoutUrl;
        }
    }
}
