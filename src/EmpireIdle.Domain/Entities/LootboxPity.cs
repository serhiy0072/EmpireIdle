namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Лічильник гарантії гравця в скрині (GDD §9.12). Окремо від BannerPityProgress: у банера
    /// дві гарантії й 50/50, у скрині — одна, і ключ — сама скриня, а не група банерів.
    /// </summary>
    public class LootboxPity : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Ключ скрині.</summary>
        public string LootboxKey { get; private set; } = null!;

        /// <summary>Відкриттів поспіль без позиції гарантії.</summary>
        public int Since { get; private set; }

        /// <summary>Скільки всього відкрито — для аналітики й підтримки.</summary>
        public int TotalOpened { get; private set; }

        /// <summary>Токен паралелізму: два одночасні відкриття інакше з'їли б одну гарантію двічі.</summary>
        public uint Version { get; private set; }

        public LootboxPity(Guid id, Guid playerId, string lootboxKey) : base(id)
        {
            PlayerId = playerId;
            LootboxKey = lootboxKey;
        }

        protected LootboxPity() { } // Для EF Core

        /// <summary>Записує відкриття. Правила лічильника живуть у LootboxRoller.</summary>
        public void Apply(int since)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(since);

            Since = since;
            TotalOpened++;
        }
    }
}
