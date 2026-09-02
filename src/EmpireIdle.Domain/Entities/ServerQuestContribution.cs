namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Внесок одного гравця в серверний квест.
    /// LastContributedAt розв'язує нічию: хто набрав раніше, той вище в ранзі.
    /// </summary>
    public class ServerQuestContribution : Entity
    {
        public int ServerId { get; private set; }
        public string QuestKey { get; private set; } = null!;
        public Guid PlayerId { get; private set; }

        public long Amount { get; private set; }
        public DateTime LastContributedAt { get; private set; }

        /// <summary>
        /// Коли гравцю видали нагороду за цей квест. null — ще не видавали.
        ///
        /// Позначка на внеску, а не окрема таблиця: видача одноразова
        /// й прив'язана рівно до цього рядка, а повторний прогін джоба
        /// має її пропустити.
        /// </summary>
        public DateTime? RewardedAt { get; private set; }

        public ServerQuestContribution(Guid id, int serverId, string questKey, Guid playerId) : base(id)
        {
            ServerId = serverId;
            QuestKey = questKey;
            PlayerId = playerId;
        }

        protected ServerQuestContribution() { } // для EF Core

        public void Add(long amount, DateTime utcNow)
        {
            Amount += amount;
            LastContributedAt = utcNow;
        }

        /// <summary>Фіксує видачу нагороди. Повторний виклик нічого не змінює.</summary>
        public bool MarkRewarded(DateTime utcNow)
        {
            if (RewardedAt is not null)
                return false;

            RewardedAt = utcNow;
            return true;
        }

        /// <summary>Фіксує видачу нагороди. Повторний виклик нічого не змінює.</summary>
        public bool MarkRewarded(int rank, DateTime utcNow)
        {
            if (RewardedAt is not null)
                return false;

            RewardedAt = utcNow;

            RaiseDomainEvent(new Events.ServerQuestRewarded(PlayerId, QuestKey, rank, Amount, utcNow));

            return true;
        }
    }
}
