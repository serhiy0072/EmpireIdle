using EmpireIdle.Domain.Events;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Кланова споруда на карті (GDD §7.2). Поки будується — лише займає клітину;
    /// добудована дає сталий бонус усім військам клану в своєму радіусі.
    ///
    /// Готовність рахується від часу, а не прапорцем зі сканера: споруда діє,
    /// щойно настав CompletesAt. Марші клану лише зсувають цей момент.
    /// Гарнізон — окремий агрегат Garrison з господарем-спорудою.
    /// </summary>
    public class ClanStructure : Entity
    {
        #region Стан

        public int ServerId { get; private set; }

        public Guid ClanId { get; private set; }

        public int X { get; private set; }

        public int Y { get; private set; }

        /// <summary>Гарнізон споруди: юніти членів клану як підкріплення.</summary>
        public Guid GarrisonId { get; private set; }

        /// <summary>Хто заклав — для журналу й сповіщень, не для прав.</summary>
        public Guid PlacedBy { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>Повна тривалість будівництва без допомоги — від неї рахується частка маршу.</summary>
        public TimeSpan BuildDuration { get; private set; }

        /// <summary>Коли споруда запрацює. Марші клану зсувають цей момент ближче.</summary>
        public DateTime CompletesAt { get; private set; }

        /// <summary>Яку частку будівництва вже зрізали марші.</summary>
        public double AcceleratedShare { get; private set; }

        public DateTime UpdatedAt { get; private set; }

        /// <summary>Concurrency token (PostgreSQL xmin): два марші прибувають одночасно.</summary>
        public uint Version { get; private set; }

        #endregion

        #region Створення

        public ClanStructure(Guid id, int serverId, Guid clanId, int x, int y, Guid garrisonId, Guid placedBy,
            TimeSpan buildDuration, DateTime utcNow) : base(id)
        {
            if (buildDuration < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(buildDuration), "Build duration cannot be negative.");

            ServerId = serverId;
            ClanId = clanId;
            X = x;
            Y = y;
            GarrisonId = garrisonId;
            PlacedBy = placedBy;
            CreatedAt = utcNow;
            BuildDuration = buildDuration;
            CompletesAt = utcNow + buildDuration;
            UpdatedAt = utcNow;
        }

        protected ClanStructure() { } // для EF Core

        #endregion

        #region Читання

        /// <summary>Чи вже добудована: бонус і покриття діють лише з цього моменту.</summary>
        public bool IsActiveAt(DateTime utcNow) => utcNow >= CompletesAt;

        /// <summary>Чи клітина в радіусі споруди. Квадрат, як кільця карти: радіус 5 — це 11×11.</summary>
        public bool Covers(int x, int y, int radius) => Math.Max(Math.Abs(x - X), Math.Abs(y - Y)) <= radius;

        #endregion

        #region Будівництво

        /// <summary>
        /// Марш клану прискорює будівництво на частку, пропорційну своїй силі.
        /// Повертає, скільки реально зрізано: понад стелю й після добудови — нуль.
        /// </summary>
        /// <param name="share">Частка повного будівництва, яку дає цей марш.</param>
        /// <param name="maxShare">Стеля сумарного прискорення (1 — клан може добудувати миттєво).</param>
        public double Accelerate(double share, double maxShare, DateTime utcNow)
        {
            if (share < 0)
                throw new ArgumentOutOfRangeException(nameof(share), "Share cannot be negative.");

            if (IsActiveAt(utcNow))
                return 0;

            var applied = Math.Min(share, Math.Max(0, maxShare - AcceleratedShare));

            if (applied <= 0)
                return 0;

            AcceleratedShare += applied;

            var completesAt = CompletesAt - BuildDuration * applied;
            CompletesAt = completesAt < utcNow ? utcNow : completesAt;
            UpdatedAt = utcNow;

            return applied;
        }

        #endregion

        #region Оборона

        /// <summary>Споруду зруйновано. Саме видалення — справа того, хто викликав.</summary>
        public void MarkDestroyed(DateTime utcNow)
            => RaiseDomainEvent(new ClanStructureDestroyed(Id, ClanId, X, Y, utcNow));

        #endregion
    }
}
