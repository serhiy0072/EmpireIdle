using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Звіринець гравця (GDD §5.10): приручені звірі й лічильники гарантії.
    /// Окремий агрегат, а не частина села: звірі належать гравцеві назавжди,
    /// а приручення й годування не мають чекати на xmin села.
    /// Інваріант «видів не більше, ніж місць» тримає тут, а місткість —
    /// число від рівня будівлі — приходить параметром.
    /// </summary>
    public class BeastPen : Entity
    {
        private readonly List<Beast> _beasts = new();
        private readonly List<BeastTamingPity> _pity = new();

        public Guid PlayerId { get; private set; }
        public int ServerId { get; private set; }

        /// <summary>Зміна лише дочірніх рядків не чіпає корінь — без цього xmin не захистив би приручення.</summary>
        public DateTime UpdatedAt { get; private set; }

        public IReadOnlyCollection<Beast> Beasts => _beasts.AsReadOnly();
        public IReadOnlyCollection<BeastTamingPity> Pity => _pity.AsReadOnly();

        public BeastPen(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected BeastPen() { } // Для EF Core

        public bool Has(string beastKey) => _beasts.Any(b => b.BeastKey == beastKey);

        /// <summary>Місце є, якщо вид уже живе тут (дублікат місця не займає) або є вільне.</summary>
        public bool HasRoomFor(string beastKey, int capacity) => Has(beastKey) || _beasts.Count < capacity;

        /// <summary>Скільки перемог поспіль без звіра цього типу.</summary>
        public int MissesFor(string beastKey) => _pity.FirstOrDefault(p => p.BeastKey == beastKey)?.Misses ?? 0;

        /// <summary>
        /// Підсумок перемоги з наміром «Приручити». Кидок робить викликач,
        /// гарантію й місце перевіряє агрегат.
        /// </summary>
        /// <param name="rolled">Чи вдався кидок шансу.</param>
        /// <param name="pityWins">Після стількох промахів поспіль приручення гарантоване.</param>
        /// <param name="capacity">Місць у звіринці зараз — за час маршу воно могло заповнитись.</param>
        /// <param name="maxRank">Стеля рангу: дублікат понад неї звіра не дає.</param>
        public TameOutcome ResolveTaming(string beastKey, bool rolled, int pityWins, int capacity, int maxRank,
            DateTime utcNow)
        {
            // Без місця для нового виду спроба не рахується: гарантія не має згоріти на повному звіринці
            if (!HasRoomFor(beastKey, capacity))
                return TameOutcome.NoRoom;

            var pity = PityFor(beastKey);

            if (!rolled && pity.Misses < pityWins)
            {
                pity.Miss();
                Touch(utcNow);
                return TameOutcome.Missed;
            }

            var beast = _beasts.FirstOrDefault(b => b.BeastKey == beastKey);

            if (beast is not null && beast.Rank >= maxRank)
                return TameOutcome.RankCapped;

            pity.Reset();
            Touch(utcNow);

            if (beast is null)
            {
                _beasts.Add(new Beast(Guid.NewGuid(), Id, beastKey, utcNow));
                return TameOutcome.Tamed;
            }

            beast.RankUp();
            return TameOutcome.RankedUp;
        }

        private BeastTamingPity PityFor(string beastKey)
        {
            var pity = _pity.FirstOrDefault(p => p.BeastKey == beastKey);

            if (pity is null)
            {
                pity = new BeastTamingPity(Guid.NewGuid(), Id, beastKey);
                _pity.Add(pity);
            }

            return pity;
        }

        private void Touch(DateTime utcNow) => UpdatedAt = utcNow;
    }

    /// <summary>Приручений звір. Ранг піднімає дублікат (GDD §5.10).</summary>
    public class Beast : Entity
    {
        public Guid BeastPenId { get; private set; }
        public string BeastKey { get; private set; } = null!;
        public int Rank { get; private set; }
        public DateTime TamedAt { get; private set; }

        internal Beast(Guid id, Guid beastPenId, string beastKey, DateTime utcNow) : base(id)
        {
            BeastPenId = beastPenId;
            BeastKey = beastKey;
            Rank = 1;
            TamedAt = utcNow;
        }

        protected Beast() { } // Для EF Core

        internal void RankUp() => Rank++;
    }

    /// <summary>Перемоги поспіль без звіра одного типу — лічильник гарантії.</summary>
    public class BeastTamingPity : Entity
    {
        public Guid BeastPenId { get; private set; }
        public string BeastKey { get; private set; } = null!;
        public int Misses { get; private set; }

        internal BeastTamingPity(Guid id, Guid beastPenId, string beastKey) : base(id)
        {
            BeastPenId = beastPenId;
            BeastKey = beastKey;
        }

        protected BeastTamingPity() { } // Для EF Core

        internal void Miss() => Misses++;

        internal void Reset() => Misses = 0;
    }
}
