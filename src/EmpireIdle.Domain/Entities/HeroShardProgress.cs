namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Накопичені уламки конкретного героя. Окрема таблиця, а не предмет
    /// інвентаря: ключ на кожного героя роздув би конфіг предметів і виставив
    /// би уламки на шлях «використати предмет», а потім і на ринок.
    ///
    /// Рядок живе й далі після призову — гравець продовжує збирати на сузір'я.
    /// </summary>
    public class HeroShardProgress : Entity
    {
        public Guid PlayerId { get; private set; }
        public int ServerId { get; private set; }
        public string HeroKey { get; private set; } = null!;
        public int Count { get; private set; }

        public HeroShardProgress(Guid id, Guid playerId, int serverId, string heroKey) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            HeroKey = heroKey;
            Count = 0;
        }

        protected HeroShardProgress() { } // Для EF Core

        public void Add(int count)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), "Shard count must be positive.");

            Count += count;
        }

        /// <summary>
        /// Знімає вартість призову. Повертає false, якщо ще не набралось —
        /// рішення, чи це помилка, ухвалює викликач.
        /// </summary>
        public bool TryConsume(int required)
        {
            if (Count < required)
                return false;

            Count -= required;
            return true;
        }
    }
}
