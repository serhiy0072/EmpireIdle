using System.Security.Cryptography;
using System.Text;
using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Payments.Contracts;
using EmpireIdle.Infrastructure.Payments;
using Microsoft.Extensions.Options;
using Stripe;

namespace EmpireIdle.Api.Tests.Payments;

/// <summary>
/// Розбір вебхуків Stripe Checkout. Відкладені методи оплати (SEPA, переказ) приходять
/// двома подіями: completed з «unpaid», а гроші — окремо async_payment_succeeded чи _failed.
/// </summary>
public class StripeWebhookParsingTests
{
    private const string WebhookSecret = "whsec_test_secret";

    private static StripePaymentProvider Provider() => new(Options.Create(new StripeSettings
    {
        SecretKey = "sk_test_unused",
        WebhookSecret = WebhookSecret,
        SuccessUrl = "http://localhost/success",
        CancelUrl = "http://localhost/cancel"
    }), new StripeClient("sk_test_unused"));

    private static string Payload(string type, string paymentStatus) => $$"""
        {
          "id": "evt_test_1",
          "object": "event",
          "api_version": "{{StripeConfiguration.ApiVersion}}",
          "created": {{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}},
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "cs_test_1",
              "object": "checkout.session",
              "payment_status": "{{paymentStatus}}"
            }
          }
        }
        """;

    /// <summary>Заголовок Stripe-Signature: t=час,v1=HMAC-SHA256(секрет, "час.тіло").</summary>
    private static string Sign(string payload, string secret = WebhookSecret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));

        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    private static PaymentWebhookResult Parse(string type, string paymentStatus)
    {
        var payload = Payload(type, paymentStatus);
        return Provider().ParseWebhook(payload, Sign(payload));
    }

    [Theory]
    [InlineData(EventTypes.CheckoutSessionCompleted, "paid", PaymentWebhookOutcome.Paid)]
    [InlineData(EventTypes.CheckoutSessionCompleted, "unpaid", PaymentWebhookOutcome.Ignored)]
    [InlineData(EventTypes.CheckoutSessionAsyncPaymentSucceeded, "paid", PaymentWebhookOutcome.Paid)]
    [InlineData(EventTypes.CheckoutSessionAsyncPaymentFailed, "unpaid", PaymentWebhookOutcome.Failed)]
    [InlineData(EventTypes.CheckoutSessionExpired, "unpaid", PaymentWebhookOutcome.Failed)]
    public void Parse_ShouldMapTheCheckoutEvent(string type, string paymentStatus, PaymentWebhookOutcome expected)
    {
        var result = Parse(type, paymentStatus);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == PaymentWebhookOutcome.Ignored ? null : "cs_test_1", result.SessionId);
    }

    [Fact]
    public void Parse_ShouldIgnore_AnUnrelatedEvent()
        => Assert.Equal(PaymentWebhookResult.Ignored, Parse("customer.created", "paid"));

    [Fact]
    public void Parse_ShouldReject_AForgedSignature()
    {
        var payload = Payload(EventTypes.CheckoutSessionCompleted, "paid");

        Assert.Throws<InvalidWebhookSignatureException>(
            () => Provider().ParseWebhook(payload, Sign(payload, "whsec_attacker")));
    }
}
