using EmpireIdle.Application.Interfaces;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace EmpireIdle.Infrastructure.Payments
{
    /// <summary>Платіжний шлюз на Stripe Checkout.</summary>
    public class StripePaymentProvider : IPaymentProvider
    {
        private readonly StripeSettings _settings;

        public StripePaymentProvider(IOptions<StripeSettings> settings)
        {
            _settings = settings.Value;
            StripeConfiguration.ApiKey = _settings.SecretKey;
        }


        /// <inheritdoc/>
        public async Task<PaymentSession> CreateSessionAsync(
            string packKey, string displayName, int amountCents, string currency,
            Guid playerId, CancellationToken cancellationToken = default)
        {
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = _settings.SuccessUrl,
                CancelUrl = _settings.CancelUrl,
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = currency,
                            UnitAmount = amountCents,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = displayName
                            }
                        }
                    }
                },
                // Метадані повернуться у вебхуці — так ми знаємо, кому й що зараховувати
                Metadata = new Dictionary<string, string>
                {
                    ["playerId"] = playerId.ToString(),
                    ["packKey"] = packKey
                }
            };

            var session = await new SessionService().CreateAsync(options, cancellationToken: cancellationToken);

            return new PaymentSession(session.Id, session.Url);
        }

        /// <inheritdoc/>
        public PaymentWebhookResult ParseWebhook(string payload, string signatureHeader)
        {
            // Перевірка підпису: без неї будь-хто міг би надіслати «оплата пройшла»
            var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _settings.WebhookSecret);

            if (stripeEvent.Type != EventTypes.CheckoutSessionCompleted)
                return new PaymentWebhookResult(IsPaymentCompleted: false, SessionId: null);

            if (stripeEvent.Data.Object is not Session session)
                throw new InvalidOperationException($"Event {stripeEvent.Id} of type {stripeEvent.Type} does not carry a Checkout Session.");

            return new PaymentWebhookResult(
                IsPaymentCompleted: session.PaymentStatus == "paid",
                SessionId: session.Id);
        }
    }
}
