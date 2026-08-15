namespace EmpireIdle.Infrastructure.Persistence.Outbox
{
    /// <summary>
    /// Доменна подія, збережена в тій самій транзакції, що й зміна стану.
    /// Публікується окремим воркером — тому втратити її неможливо.
    /// </summary>
    public class OutboxMessage
    {
        public Guid Id { get; set; }

        /// <summary>Повне ім'я типу події для десеріалізації.</summary>
        public string Type { get; set; } = null!;

        /// <summary>Подія у форматі JSON.</summary>
        public string Payload { get; set; } = null!;

        public DateTime OccurredAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        /// <summary>Кількість спроб публікації.</summary>
        public int Attempts { get; set; }

        /// <summary>Текст останньої помилки, якщо публікація впала.</summary>
        public string? Error { get; set; }
    }
}
