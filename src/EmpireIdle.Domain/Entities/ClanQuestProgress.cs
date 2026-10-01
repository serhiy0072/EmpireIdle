using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Спільний прогрес кланового квесту (GDD §7.2). Один рядок на клан і квест:
    /// клан — до двохсот гравців, тож прямий інкремент під токеном дешевший за
    /// джоб підрахунку, як у серверних квестів. Прогрес належить клану й
    /// лишається в ньому, коли учасник виходить.
    /// </summary>
    public class ClanQuestProgress : Entity
    {
        public int ServerId { get; private set; }
        public Guid ClanId { get; private set; }
        public string QuestKey { get; private set; } = null!;

        public long Amount { get; private set; }
        public long Target { get; private set; }

        public QuestState State { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin): внески учасників приходять паралельно.</summary>
        public uint Version { get; private set; }

        public ClanQuestProgress(Guid id, int serverId, Guid clanId, string questKey, long target) : base(id)
        {
            if (target <= 0)
                throw new ArgumentOutOfRangeException(nameof(target), "Quest target must be positive.");

            ServerId = serverId;
            ClanId = clanId;
            QuestKey = questKey;
            Target = target;
            State = QuestState.InProgress;
        }

        protected ClanQuestProgress() { } // для EF Core

        /// <summary>Додає внесок учасника. Повертає true, якщо квест щойно завершився.</summary>
        public bool Add(long amount, DateTime utcNow)
        {
            if (amount <= 0 || State != QuestState.InProgress)
                return false;

            Amount = Math.Min(Target, Amount + amount);

            if (Amount < Target)
                return false;

            State = QuestState.Completed;
            CompletedAt = utcNow;

            return true;
        }
    }
}
