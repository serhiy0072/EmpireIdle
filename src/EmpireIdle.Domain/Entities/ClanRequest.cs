using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Заявка на вступ або запрошення в клан.
    ///
    /// Рядок не видаляється при вирішенні: історія потрібна, щоб
    /// відрізнити «ніколи не подавав» від «відхилили годину тому»,
    /// і щоб кулдаун після відмови мав на чому триматись.
    /// </summary>
    public class ClanRequest : Entity
    {
        public int ServerId { get; private set; }

        public Guid ClanId { get; private set; }

        public Guid PlayerId { get; private set; }

        public ClanRequestKind Kind { get; private set; }

        public ClanRequestStatus Status { get; private set; }

        /// <summary>Хто надіслав запрошення або вирішив заявку; null, поки заявка чекає.</summary>
        public Guid? ResolvedBy { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime ExpiresAt { get; private set; }

        public DateTime? ResolvedAt { get; private set; }

        /// <summary>Чекає рішення й ще не протермінована.</summary>
        public bool IsPending(DateTime utcNow) => Status == ClanRequestStatus.Pending && ExpiresAt > utcNow;

        public ClanRequest(Guid id, int serverId, Guid clanId, Guid playerId,
            ClanRequestKind kind, DateTime expiresAt, DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            ClanId = clanId;
            PlayerId = playerId;
            Kind = kind;
            Status = ClanRequestStatus.Pending;
            ExpiresAt = expiresAt;
            CreatedAt = utcNow;
        }

        protected ClanRequest() { } // для EF Core

        /// <summary>
        /// Прийняти. Вступ у клан робить агрегат клану — тут лише
        /// закривається сама заявка.
        /// </summary>
        /// <param name="actorId">
        /// Офіцер для заявки, сам гравець для запрошення.
        /// Перевірку прав робить хендлер: ролі живуть в іншому агрегаті.
        /// </param>
        public void Accept(Guid actorId, DateTime utcNow) => Resolve(ClanRequestStatus.Accepted, actorId, utcNow);

        /// <summary>Відхилити: офіцер відмовляє заявнику або гравець — запрошенню.</summary>
        public void Decline(Guid actorId, DateTime utcNow) => Resolve(ClanRequestStatus.Declined, actorId, utcNow);

        /// <summary>Зняти власну заявку або відкликати надіслане запрошення.</summary>
        public void Cancel(Guid actorId, DateTime utcNow) => Resolve(ClanRequestStatus.Cancelled, actorId, utcNow);

        private void Resolve(ClanRequestStatus status, Guid actorId, DateTime utcNow)
        {
            if (Status != ClanRequestStatus.Pending)
                throw new InvalidStateException($"Clan request {Id} is already {Status}.");

            // Протермінована заявка не приймається, але й не «зникає»:
            // її закриває той самий перехід, лише іншим статусом
            if (ExpiresAt <= utcNow && status == ClanRequestStatus.Accepted)
                throw new RequirementNotMetException("This request has expired.");

            Status = status;
            ResolvedBy = actorId;
            ResolvedAt = utcNow;
        }
    }
}
