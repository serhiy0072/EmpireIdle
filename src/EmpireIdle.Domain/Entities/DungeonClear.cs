namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Відмітка «цей рівень цього данжу пройдено». Рівень 2 відкривається
    /// після зачистки першого, рівень 3 — після другого: гейт зберігається
    /// рядком, а не полем у гравці, бо данжів десять і кожен іде своїм темпом.
    /// </summary>
    public class DungeonClear : Entity
    {
        public Guid PlayerId { get; private set; }

        public string DungeonKey { get; private set; } = null!;

        public int Level { get; private set; }

        public DateTime ClearedAt { get; private set; }

        public DungeonClear(Guid id, Guid playerId, string dungeonKey, int level, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            DungeonKey = dungeonKey;
            Level = level;
            ClearedAt = utcNow;
        }

        protected DungeonClear() { } // для EF Core
    }
}
