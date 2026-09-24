using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>Повідомлення чату й кеш їхніх перекладів. Усе — в межах поточного світу.</summary>
    public interface IChatRepository
    {
        Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

        Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Скільки повідомлень гравець надіслав після моменту — для антиспаму.</summary>
        Task<int> CountSentSinceAsync(Guid senderId, DateTime since, CancellationToken cancellationToken = default);

        /// <summary>Найстаріше з повідомлень гравця після моменту — щоб сказати, скільки чекати.</summary>
        Task<DateTime?> GetOldestSentSinceAsync(Guid senderId, DateTime since, CancellationToken cancellationToken = default);

        /// <summary>Останні повідомлення серверного каналу до моменту, старші першими.</summary>
        Task<List<ChatMessage>> GetServerHistoryAsync(DateTime? before, int take, CancellationToken cancellationToken = default);

        Task<List<ChatMessage>> GetClanHistoryAsync(Guid clanId, DateTime? before, int take, CancellationToken cancellationToken = default);

        /// <summary>Приватна розмова двох гравців, в обидва боки.</summary>
        Task<List<ChatMessage>> GetPrivateHistoryAsync(Guid playerId, Guid partnerId, DateTime? before, int take,
            CancellationToken cancellationToken = default);

        /// <summary>Останнє повідомлення кожної приватної розмови гравця, найсвіжіші першими.</summary>
        Task<List<ChatMessage>> GetLatestPrivateMessagesAsync(Guid playerId, int take, CancellationToken cancellationToken = default);

        /// <summary>Уже збережені переклади цих повідомлень на мову.</summary>
        Task<Dictionary<Guid, string>> GetTranslationsAsync(IReadOnlyCollection<Guid> messageIds, string language,
            CancellationToken cancellationToken = default);

        Task AddTranslationAsync(ChatTranslation translation, CancellationToken cancellationToken = default);

        /// <summary>Прибирає повідомлення, старші за момент, разом із перекладами. Повертає кількість.</summary>
        Task<int> DeleteOlderThanAsync(DateTime before, CancellationToken cancellationToken = default);
    }
}
