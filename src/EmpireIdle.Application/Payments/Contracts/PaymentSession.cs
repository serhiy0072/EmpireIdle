namespace EmpireIdle.Application.Payments.Contracts
{
    /// <summary>Створена сесія оплати.</summary>
    public record PaymentSession(string SessionId, string CheckoutUrl);
}
