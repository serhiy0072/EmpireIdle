using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Payments.Contracts;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace EmpireIdle.Infrastructure.Payments
{
    /// <summary>Платіжний шлюз на Stripe Checkout.</summary>
    public class StripePaymentProvider : IPaymentProvider
    {
        private readonly StripeSettings _settings;
        private readonly IStripeClient _client;

        /// <param name="client">
        /// Один клієнт на процес: він тримає HTTP-з'єднання й ключ. Глобальний
        /// StripeConfiguration.ApiKey з конструктора перезаписувався б на кожен scope.
        /// </param>
        public StripePaymentProvider(IOptions<StripeSettings> settings, IStripeClient client)
        {
            _settings = settings.Value;
            _client = client;
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

            var session = await new SessionService(_client).CreateAsync(options, cancellationToken: cancellationToken);

            return new PaymentSession(session.Id, session.Url);
        }

        /// <inheritdoc/>
        public PaymentWebhookResult ParseWebhook(string payload, string signatureHeader)
        {
            Stripe.Event stripeEvent;

            try
            {
                // Перевірка підпису: без неї будь-хто міг би надіслати «оплата пройшла»
                stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _settings.WebhookSecret);
            }
            catch (StripeException ex)
            {
                throw new InvalidWebhookSignatureException("Stripe webhook signature validation failed.", ex);
            }

            // Не наша подія — не помилка: віддаємо 200, щоб Stripe не ретраїв
            if (stripeEvent.Type is not (EventTypes.CheckoutSessionCompleted
                                         or EventTypes.CheckoutSessionAsyncPaymentSucceeded
                                         or EventTypes.CheckoutSessionAsyncPaymentFailed
                                         or EventTypes.CheckoutSessionExpired))
                return PaymentWebhookResult.Ignored;

            if (stripeEvent.Data.Object is not Session session)
                throw new InvalidOperationException($"Event {stripeEvent.Id} of type {stripeEvent.Type} does not carry a Checkout Session.");

            return stripeEvent.Type switch
            {
                // Відкладений метод (SEPA, банківський переказ): completed приходить з «unpaid»,
                // а гроші — пізніше окремою подією async_payment_succeeded
                EventTypes.CheckoutSessionCompleted when session.PaymentStatus != "paid" => PaymentWebhookResult.Ignored,
                EventTypes.CheckoutSessionCompleted or EventTypes.CheckoutSessionAsyncPaymentSucceeded
                    => new PaymentWebhookResult(PaymentWebhookOutcome.Paid, session.Id),
                _ => new PaymentWebhookResult(PaymentWebhookOutcome.Failed, session.Id)
            };
        }
    }
}
