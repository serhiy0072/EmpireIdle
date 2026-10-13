
using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Application.Interfaces
{
    /// <summary>
    /// Репозиторій для роботи з Player entity.
    /// </summary>
    public interface IPlayerRepository : IRepository<Player>
    {
        /// <summary>Гравець акаунта на конкретному сервері.</summary>
        Task<Player?> GetByUserIdAsync(string userId, int serverId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Id гравців поточного світу за зростанням, починаючи після <paramref name="after"/>.
        /// Keyset, а не Skip: розсилка на тисячі гравців іде пачками, і зсув не плаває, коли хтось реєструється.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetIdsAfterAsync(Guid? after, int take, CancellationToken cancellationToken = default);

        /// <summary>Члени клану — адресати кланового чату в момент доставки.</summary>
        Task<IReadOnlyList<Guid>> GetIdsByClanAsync(Guid clanId, CancellationToken cancellationToken = default);

        /// <summary>Імена гравців за списком id — для топу, одним запитом.</summary>
        Task<Dictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Позначає гравця присутнім, якщо попередня позначка старша за поріг.
        /// ExecuteUpdate, бо викликається з пайплайну на кожен запит: тягнути
        /// агрегат і зберігати його там, де запит нічого не змінює, не можна.
        /// </summary>
        /// <returns>true, якщо рядок оновлено.</returns>
        Task<bool> TouchLastSeenAsync(Guid playerId, DateTime utcNow, TimeSpan threshold, CancellationToken cancellationToken = default);

        /// <summary>Час останньої присутності вказаних гравців.</summary>
        Task<Dictionary<Guid, DateTime>> GetLastSeenAsync(IReadOnlyCollection<Guid> playerIds, CancellationToken cancellationToken = default);
    }
}
