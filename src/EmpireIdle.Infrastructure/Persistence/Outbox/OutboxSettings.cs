namespace EmpireIdle.Infrastructure.Persistence.Outbox
{
    /// <summary>Налаштування воркера Outbox. Це інфраструктура, не ігровий баланс.</summary>
    public class OutboxSettings
    {
        /// <summary>Пауза між прогонами, секунди.</summary>
        public int PollSeconds { get; set; } = 5;

        /// <summary>Скільки повідомлень брати за прогін.</summary>
        public int BatchSize { get; set; } = 50;

        /// <summary>Після скількох невдач повідомлення відкладається назавжди.</summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>Скільки днів тримати оброблені повідомлення.</summary>
        public int RetentionDays { get; set; } = 7;
    }
}
