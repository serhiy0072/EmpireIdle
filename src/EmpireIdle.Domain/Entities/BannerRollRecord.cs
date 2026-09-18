using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Журнал роллів: рядок на кожне прокручування банера.
    ///
    /// Зберігаємо сід і стан pity ДО ролла, а не сам результат-як-факт:
    /// цього достатньо, щоб переграти ролл один в один і показати гравцеві,
    /// що гарантія спрацювала саме там, де мала (§6.2).
    ///
    /// Append-only: рядки ніколи не оновлюються.
    /// </summary>
    public class BannerRollRecord : Entity
    {
        public Guid PlayerId { get; private set; }

        /// <summary>Світ, у який пішла нагорода. Самі gems акаунтні, а герой і спорядження — ні.</summary>
        public int ServerId { get; private set; }

        public string BannerKey { get; private set; } = null!;

        public string PityGroup { get; private set; } = null!;

        public string DropKey { get; private set; } = null!;

        public Rarity Rarity { get; private set; }

        /// <summary>Сід ролла: разом зі станом нижче дає повне відтворення.</summary>
        public int Seed { get; private set; }

        public int RareSinceBefore { get; private set; }

        public int UniqueSinceBefore { get; private set; }

        public bool FeaturedGuaranteedBefore { get; private set; }

        /// <summary>Дроп видала гарантія, а не вага.</summary>
        public bool WasPity { get; private set; }

        public bool LostFiftyFifty { get; private set; }

        /// <summary>Ціна на момент ролла: конфіг змінюється, журнал — ні.</summary>
        public int PriceGems { get; private set; }

        public DateTime RolledAt { get; private set; }

        public BannerRollRecord(
            Guid id,
            Guid playerId,
            int serverId,
            string bannerKey,
            string pityGroup,
            int priceGems,
            int seed,
            PityState before,
            BannerRollResult result,
            DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            BannerKey = bannerKey;
            PityGroup = pityGroup;
            PriceGems = priceGems;
            Seed = seed;

            RareSinceBefore = before.RareSince;
            UniqueSinceBefore = before.UniqueSince;
            FeaturedGuaranteedBefore = before.FeaturedGuaranteed;

            DropKey = result.Drop.Key;
            Rarity = result.Drop.Rarity;
            WasPity = result.WasPity;
            LostFiftyFifty = result.LostFiftyFifty;

            RolledAt = utcNow;
        }

        protected BannerRollRecord() { } // Для EF Core
    }
}
