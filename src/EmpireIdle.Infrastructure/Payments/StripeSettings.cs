namespace EmpireIdle.Infrastructure.Payments
{
    /// <summary>Налаштування Stripe. Ключі — лише через User Secrets.</summary>
    public class StripeSettings
    {
        /// <summary>Секретний ключ API (sk_test_... / sk_live_...).</summary>
        public string SecretKey { get; set; } = null!;

        /// <summary>Секрет для перевірки підпису вебхука (whsec_...).</summary>
        public string WebhookSecret { get; set; } = null!;

        /// <summary>Куди повертати гравця після успішної оплати.</summary>
        public string SuccessUrl { get; set; } = null!;

        /// <summary>Куди повертати після скасування.</summary>
        public string CancelUrl { get; set; } = null!;
    }
}