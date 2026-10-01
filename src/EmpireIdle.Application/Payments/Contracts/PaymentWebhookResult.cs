namespace EmpireIdle.Application.Payments.Contracts
{
    /// <summary>Результат розбору вебхука від провайдера. SessionId — лише для Paid і Failed.</summary>
    public record PaymentWebhookResult(PaymentWebhookOutcome Outcome, string? SessionId)
    {
        public static PaymentWebhookResult Ignored { get; } = new(PaymentWebhookOutcome.Ignored, null);
    }

    /// <summary>Що вебхук означає для платежу.</summary>
    public enum PaymentWebhookOutcome
    {
        /// <summary>Не стосується стану платежу (чужа подія або оплата ще в дорозі) — 200 без дій.</summary>
        Ignored = 0,

        /// <summary>Гроші отримано — нараховуємо gems.</summary>
        Paid = 1,

        /// <summary>Оплата не відбулась (відхилений відкладений платіж, прострочена сесія).</summary>
        Failed = 2
    }
}
