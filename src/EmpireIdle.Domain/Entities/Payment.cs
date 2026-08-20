namespace EmpireIdle.Domain.Entities
{
    /// <summary>Стан платежу.</summary>
    public enum PaymentStatus
    {
        /// <summary>Сесію створено, гравець ще не заплатив.</summary>
        Pending = 1,

        /// <summary>Оплату підтверджено вебхуком, gems зараховані.</summary>
        Completed = 2,

        /// <summary>Оплата не відбулась (скасування, помилка, прострочення).</summary>
        Failed = 3
    }

    /// <summary>
    /// Платіж за пакет gems. Створюється при відкритті Checkout,
    /// завершується вебхуком від Stripe — не поверненням гравця на сайт.
    /// </summary>
    public class Payment : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Ключ пакета з конфіга.</summary>
        public string PackKey { get; private set; } = null!;

        /// <summary>Скільки gems має отримати гравець.</summary>
        public int Gems { get; private set; }

        /// <summary>Сума в центах — фіксуємо на момент покупки, бо ціни в конфізі можуть змінитись.</summary>
        public int AmountCents { get; private set; }

        /// <summary>Валюта (usd).</summary>
        public string Currency { get; private set; } = null!;

        /// <summary>Id сесії Stripe Checkout — зв'язок із вебхуком.</summary>
        public string SessionId { get; private set; } = null!;

        public PaymentStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>Коли підтверджено; null — ще ні.</summary>
        public DateTime? CompletedAt { get; private set; }

        /// <summary>
        /// Світ, у якому зроблено покупку. Вебхук анонімний і не має токена,
        /// тож контекст для query-фільтрів відновлюється звідси.
        /// </summary>
        public int ServerId { get; private set; }

        public Payment(Guid id, Guid playerId, int serverId, string packKey, int gems,
            int amountCents, string currency, string sessionId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            PackKey = packKey;
            Gems = gems;
            AmountCents = amountCents;
            Currency = currency;
            SessionId = sessionId;
            Status = PaymentStatus.Pending;
            CreatedAt = utcNow;
        }

        protected Payment() { } // Для EF Core

        /// <summary>
        /// Підтверджує оплату. Повторний виклик ігнорується —
        /// Stripe може надіслати той самий вебхук кілька разів.
        /// </summary>
        /// <returns>true, якщо це перше підтвердження і gems треба зарахувати.</returns>
        public bool Complete(DateTime utcNow)
        {
            if (Status == PaymentStatus.Completed)
                return false;

            Status = PaymentStatus.Completed;
            CompletedAt = utcNow;
            return true;
        }

        /// <summary>Позначає платіж невдалим.</summary>
        public void Fail()
        {
            if (Status == PaymentStatus.Completed)
                throw new InvalidOperationException($"Payment {Id} is already completed.");

            Status = PaymentStatus.Failed;
        }
    }
}
