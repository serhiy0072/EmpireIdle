using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Особисті технології гравця (рішення 28.10.2026): вивчені рівні вузлів і одне дослідження за раз.
    /// Рівні — ключі «тір:вузол»; дослідження скорочують gems, предмети прискорення й допомога клану,
    /// а завершує його сканер таймерів або будь-яка дія гравця, що застає таймер дозрілим.
    /// </summary>
    public class PlayerResearch : Entity
    {
        public Guid PlayerId { get; private set; }

        public int ServerId { get; private set; }

        /// <summary>Вивчені рівні: «тір:вузол» → рівень.</summary>
        public IReadOnlyDictionary<string, int> Levels { get; private set; } = new Dictionary<string, int>();

        /// <summary>Вузол, що зараз досліджується («тір:вузол»); null — Академія вільна.</summary>
        public string? ActiveKey { get; private set; }

        /// <summary>Рівень, який дасть поточне дослідження.</summary>
        public int ActiveLevel { get; private set; }

        public DateTime? StartedAt { get; private set; }

        public DateTime? CompletesAt { get; private set; }

        /// <summary>Повна тривалість на старті — від неї рахується частка, яку зрізає одна допомога.</summary>
        public TimeSpan PlannedDuration { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        public uint Version { get; private set; }

        public PlayerResearch(Guid id, Guid playerId, int serverId, DateTime utcNow) : base(id)
        {
            PlayerId = playerId;
            ServerId = serverId;
            UpdatedAt = utcNow;
        }

        protected PlayerResearch() { } // для EF Core

        public bool IsResearching => ActiveKey is not null;

        public int LevelOf(TechnologyKey key) => Levels.GetValueOrDefault(key.ToString());

        /// <summary>
        /// Починає наступний рівень вузла. Друге дослідження поверх поточного — чесна відмова:
        /// гравець бачить її, коли Академію відкрито у двох вкладках.
        /// </summary>
        public void Start(TechnologyKey key, TimeSpan duration, DateTime utcNow)
        {
            if (IsResearching)
                throw new RequirementNotMetException(RefusalReasons.ResearchBusy,
                    $"Player {PlayerId} is already researching {ActiveKey}.");

            ActiveKey = key.ToString();
            ActiveLevel = LevelOf(key) + 1;
            StartedAt = utcNow;
            PlannedDuration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            CompletesAt = utcNow + PlannedDuration;
            UpdatedAt = utcNow;
        }

        /// <summary>
        /// Зрізає частину залишку (прискорення чи допомога). Не раніше за «зараз»: дозрілий таймер
        /// завершиться на наступному скануванні або діянні гравця.
        /// </summary>
        public void Shorten(TimeSpan cut, DateTime utcNow)
        {
            if (CompletesAt is not { } completesAt || cut <= TimeSpan.Zero)
                return;

            var shortened = completesAt - cut;
            CompletesAt = shortened < utcNow ? utcNow : shortened;
            UpdatedAt = utcNow;
        }

        /// <summary>Залишок дослідження; нуль — дозріло або нічого не досліджується.</summary>
        public TimeSpan Remaining(DateTime utcNow)
            => CompletesAt is { } completesAt && completesAt > utcNow ? completesAt - utcNow : TimeSpan.Zero;

        /// <summary>
        /// Записує дозрілий рівень і звільняє Академію. Повертає вивчений ключ або null, якщо ще рано
        /// чи нічого не досліджується.
        /// </summary>
        public TechnologyKey? CompleteDue(DateTime utcNow)
        {
            if (ActiveKey is not { } active || CompletesAt is not { } completesAt || completesAt > utcNow)
                return null;

            var levels = new Dictionary<string, int>(Levels) { [active] = ActiveLevel };
            Levels = levels;

            ActiveKey = null;
            ActiveLevel = 0;
            StartedAt = null;
            CompletesAt = null;
            PlannedDuration = TimeSpan.Zero;
            UpdatedAt = utcNow;

            return TechnologyKey.Parse(active);
        }
    }
}
