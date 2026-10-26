
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

        /// <summary>
        /// Відбиток параметрів запиту: той самий ключ з іншими даними — баг клієнта, а не повтор,
        /// і віддати йому чужий «успіх» означало б сказати «зроблено» про незроблене.
        /// null — запис старший за цю перевірку.
        /// </summary>
        public string? RequestHash { get; private set; }

        /// <summary>Серіалізована відповідь — віддається при повторі.</summary>
        public string? ResponseJson { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public IdempotencyRecord(Guid id, string key, Guid playerId, string requestType, string? responseJson, DateTime utcNow,
            string? requestHash = null) : base(id)
        {
            Key = key;
            PlayerId = playerId;
            RequestType = requestType;
            RequestHash = requestHash;
            ResponseJson = responseJson;
            CreatedAt = utcNow;
        }

        protected IdempotencyRecord() { } // Для EF Core
    }
}
