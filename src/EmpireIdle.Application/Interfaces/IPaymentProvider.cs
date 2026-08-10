
namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Створена сесія оплати.</summary>
    public record PaymentSession(string SessionId, string CheckoutUrl);

    /// <summary>Результат розбору вебхука від провайдера.</summary>
    public record PaymentWebhookResult(bool IsPaymentCompleted, string? SessionId);

    /// <summary>
    /// Платіжний шлюз. Абстракція дозволяє замінити Stripe на LiqPay/Paddle
    /// без змін в Application-шарі.
    /// </summary>
    public interface IPaymentProvider
    {
        /// <summary>
        /// Створює сесію оплати й повертає посилання, куди відправити гравця.
        /// </summary>
        /// <param name="packKey">Ключ пакета — передається назад у вебхуці.</param>
        /// <param name="displayName">Назва товару для сторінки оплати.</param>
        /// <param name="amountCents">Сума в мінімальних одиницях валюти.</param>
        /// <param name="currency">Валюта (usd, eur…).</param>
        /// <param name="playerId">Гравець — для звірки у вебхуці.</param>
        Task<PaymentSession> CreateSessionAsync(string packKey, string displayName, int amountCents, string currency,Guid playerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Перевіряє підпис вебхука й розбирає його вміст.
        /// Кидає виняток, якщо підпис недійсний.
        /// </summary>
        /// <param name="payload">Сире тіло запиту.</param>
        /// <param name="signatureHeader">Заголовок із підписом.</param>
        PaymentWebhookResult ParseWebhook(string payload, string signatureHeader);
    }
}
