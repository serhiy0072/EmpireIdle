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

        /// <summary>
        /// Годує звіра кормом (GDD §5.10). Бере не більше, ніж треба до стелі рівня:
        /// корм понад стелю лишається гравцю, а не згоряє.
        /// </summary>
        /// <param name="offered">Скільки корму гравець готовий віддати.</param>
        /// <param name="experienceToNext">Скільки досвіду треба з рівня L на L+1.</param>
        /// <param name="levelsPerRank">Ранг × це число — стеля рівня.</param>
        /// <returns>Скільки корму з'їдено; 0 — звір уже на стелі.</returns>
        /// <exception cref="Exceptions.EntityNotFoundException">Такого звіра в звіринці немає.</exception>
        public int Feed(string beastKey, int offered, Func<int, int> experienceToNext, int levelsPerRank, DateTime utcNow)
        {
            var beast = _beasts.FirstOrDefault(b => b.BeastKey == beastKey)
                ?? throw new Exceptions.EntityNotFoundException("Beast", beastKey);

            var eaten = beast.Feed(offered, experienceToNext, beast.Rank * levelsPerRank);

            if (eaten > 0)
                Touch(utcNow);

            return eaten;
        }

        /// <summary>
        /// Вмикає пасивку звіра (GDD §5.10): діє <paramref name="duration"/>, перезаряджається
        /// <paramref name="cooldown"/> від моменту активації. Два звірі з тим самим ефектом
        /// одночасно не діють — інакше місця звіринця множили б один бонус.
        /// </summary>
        /// <param name="effectOf">Ефект звіра за ключем — з конфіга.</param>
        public void Activate(string beastKey, Func<string, EffectTarget> effectOf, TimeSpan duration, TimeSpan cooldown,
            DateTime utcNow)
        {
            var beast = _beasts.FirstOrDefault(b => b.BeastKey == beastKey)
                ?? throw new Exceptions.EntityNotFoundException("Beast", beastKey);

            if (beast.CooldownUntil is { } readyAt && readyAt > utcNow)
                throw new Exceptions.RequirementNotMetException(Exceptions.RefusalReasons.BeastOnCooldown,
                    $"Beast '{beastKey}' is ready at {readyAt:u}.",
                    DateTime.SpecifyKind(readyAt, DateTimeKind.Utc).ToString("O"));

            var effect = effectOf(beastKey);

            if (_beasts.FirstOrDefault(b => b != beast && b.IsActiveAt(utcNow) && effectOf(b.BeastKey) == effect) is { } rival)
                throw new Exceptions.RequirementNotMetException(Exceptions.RefusalReasons.BeastEffectActive,
                    $"Beast '{rival.BeastKey}' already gives {effect}.", rival.BeastKey);

            beast.Activate(duration, cooldown, utcNow);
            Touch(utcNow);
        }

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

        /// <summary>Рівень від корму; визначає силу пасивки. Не вище ранг × LevelsPerRank.</summary>
        public int Level { get; private set; }

        /// <summary>Досвід усередині поточного рівня.</summary>
        public int Experience { get; private set; }

        public DateTime TamedAt { get; private set; }

        /// <summary>Остання активація пасивки; разом з ActiveUntil — вікно, у якому вона діяла.</summary>
        public DateTime? ActivatedAt { get; private set; }

        public DateTime? ActiveUntil { get; private set; }

        /// <summary>Коли пасивку знову можна активувати.</summary>
        public DateTime? CooldownUntil { get; private set; }

        public bool IsActiveAt(DateTime utcNow) => ActiveUntil > utcNow;

        internal Beast(Guid id, Guid beastPenId, string beastKey, DateTime utcNow) : base(id)
        {
            BeastPenId = beastPenId;
            BeastKey = beastKey;
            Rank = 1;
            Level = 1;
            TamedAt = utcNow;
        }

        protected Beast() { } // Для EF Core

        internal void RankUp() => Rank++;

        internal void Activate(TimeSpan duration, TimeSpan cooldown, DateTime utcNow)
        {
            ActivatedAt = utcNow;
            ActiveUntil = utcNow + duration;
            CooldownUntil = utcNow + cooldown;
        }

        /// <returns>Скільки корму з'їдено.</returns>
        internal int Feed(int offered, Func<int, int> experienceToNext, int maxLevel)
        {
            var eaten = 0;

            while (eaten < offered && Level < maxLevel)
            {
                var need = experienceToNext(Level) - Experience;
                var take = Math.Min(need, offered - eaten);

                Experience += take;
                eaten += take;

                if (Experience >= experienceToNext(Level))
                {
                    Level++;
                    Experience = 0;
                }
            }

            return eaten;
        }
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
