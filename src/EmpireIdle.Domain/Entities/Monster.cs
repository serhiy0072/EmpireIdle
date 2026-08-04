
namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Монстр на карті — PvE-ціль. Сила й нагороди не зберігаються:
    /// обчислюються з конфіга за типом і рівнем.
    /// </summary>
    public class Monster : Entity
    {
        /// <summary>Світ, у якому живе монстр.</summary>
        public int ServerId { get; private set; }

        /// <summary>Тип монстра (ключ із конфіга).</summary>
        public string Type { get; private set; } = null!;

        /// <summary>Рівень — множник сили й нагороди.</summary>
        public int Level { get; private set; }

        public int X { get; private set; }
        public int Y { get; private set; }

        /// <summary>Коли з'явився (для аналітики й майбутнього деспавну).</summary>
        public DateTime SpawnedAt { get; private set; }

        public Monster(Guid id, int serverId, string type, int level, int x, int y) : base(id)
        {
            ServerId = serverId;
            Type = type;
            Level = level;
            X = x;
            Y = y;
            SpawnedAt = DateTime.UtcNow;
        }

        protected Monster() { } // Для EF Core
    }
}
