namespace EmpireIdle.Application.Payments.Contracts
{
    /// <summary>Результат розбору вебхука від провайдера.</summary>
    public record PaymentWebhookResult(bool IsPaymentCompleted, string? SessionId);
}
