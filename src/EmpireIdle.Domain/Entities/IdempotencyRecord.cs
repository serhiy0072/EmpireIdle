
namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Запис про виконану операцію: захист від повторної обробки
    /// того самого запиту при ретраях клієнта.
    /// </summary>
    public class IdempotencyRecord : Entity
    {
        /// <summary>Ключ, наданий клієнтом.</summary>
        public string Key { get; private set; } = null!;

        /// <summary>Гравець, від імені якого виконано операцію.</summary>
        public Guid PlayerId { get; private set; }

        /// <summary>Тип запиту (щоб один ключ не перевикористали для іншої операції).</summary>
        public string RequestType { get; private set; } = null!;

        /// <summary>Серіалізована відповідь — віддається при повторі.</summary>
        public string? ResponseJson { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public IdempotencyRecord(Guid id, string key, Guid playerId, string requestType, string? responseJson, DateTime utcNow) : base(id)
        {
            Key = key;
            PlayerId = playerId;
            RequestType = requestType;
            ResponseJson = responseJson;
            CreatedAt = utcNow;
        }

        protected IdempotencyRecord() { } // Для EF Core
    }
}
