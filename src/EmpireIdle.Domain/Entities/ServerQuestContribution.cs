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
    }
}
