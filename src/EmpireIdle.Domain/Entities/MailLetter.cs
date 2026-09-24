using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Особистий лист у скриньці (GDD §7.4): рядок на адресата.
    ///
    /// Два різновиди. Лист-посилання — не зліпок стану: через три доби
    /// запрошення може бути вже протерміноване, а клан — розпущений; кнопки
    /// вирішує актуальний стан. Лист із нагородою несе вкладення сам: гравцю
    /// пообіцяли саме його, і ребаланс конфіга обіцянки не змінює.
    /// </summary>
    public class MailLetter : Entity
    {
        public int ServerId { get; private set; }

        public Guid PlayerId { get; private set; }

        public MailKind Kind { get; private set; }

        /// <summary>На що посилається лист; тип сутності задає Kind. Null — лист із вкладенням.</summary>
        public Guid? ReferenceId { get; private set; }

        /// <summary>Вкладення: нагороди, які гравець забирає кнопкою.</summary>
        public IReadOnlyList<MailReward> Rewards { get; private set; } = [];

        /// <summary>Порядковий номер у серії — для щоденної нагороди це день.</summary>
        public int? Sequence { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Після цього моменту лист прибирає джоб. Для листа з нагородою це й
        /// строк нагороди: незабрана згорає разом із листом.
        /// </summary>
        public DateTime ExpiresAt { get; private set; }

        public DateTime? ReadAt { get; private set; }

        public DateTime? ClaimedAt { get; private set; }

        public bool IsRead => ReadAt is not null;

        public bool HasRewards => Rewards.Count > 0;

        public bool IsClaimed => ClaimedAt is not null;

        /// <summary>Лист-посилання.</summary>
        public MailLetter(Guid id, int serverId, Guid playerId, MailKind kind, Guid referenceId, DateTime utcNow,
            TimeSpan retention) : base(id)
        {
            ServerId = serverId;
            PlayerId = playerId;
            Kind = kind;
            ReferenceId = referenceId;
            CreatedAt = utcNow;
            ExpiresAt = utcNow + retention;
        }

        private MailLetter(Guid id, int serverId, Guid playerId, MailKind kind, IReadOnlyList<MailReward> rewards,
            int? sequence, DateTime utcNow, DateTime expiresAt) : base(id)
        {
            if (rewards.Count == 0)
                throw new ArgumentException("A reward letter needs at least one reward.", nameof(rewards));

            if (expiresAt <= utcNow)
                throw new ArgumentOutOfRangeException(nameof(expiresAt), expiresAt, "A reward letter must expire in the future.");

            ServerId = serverId;
            PlayerId = playerId;
            Kind = kind;
            Rewards = rewards.ToList();
            Sequence = sequence;
            CreatedAt = utcNow;
            ExpiresAt = expiresAt;
        }

        protected MailLetter() { } // Для EF Core

        /// <summary>Лист із вкладенням, що діє до expiresAt.</summary>
        public static MailLetter WithRewards(Guid id, int serverId, Guid playerId, MailKind kind,
            IReadOnlyList<MailReward> rewards, int? sequence, DateTime utcNow, DateTime expiresAt)
            => new(id, serverId, playerId, kind, rewards, sequence, utcNow, expiresAt);

        /// <summary>Позначка прочитання. Повторне відкриття не переписує першого моменту.</summary>
        public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;

        /// <summary>Чи можна забрати вкладення зараз.</summary>
        public bool CanClaimAt(DateTime utcNow) => HasRewards && !IsClaimed && ExpiresAt > utcNow;

        /// <summary>
        /// Забирає вкладення. Видачу робить той, хто кличе: лист лише фіксує,
        /// що вкладення вже не його. Забране читається як прочитане.
        /// </summary>
        public IReadOnlyList<MailReward> Claim(DateTime utcNow)
        {
            if (!HasRewards || IsClaimed)
                throw new RequirementNotMetException(RefusalReasons.MailNothingToClaim, "This letter has nothing to claim.");

            if (ExpiresAt <= utcNow)
                throw new RequirementNotMetException(RefusalReasons.MailLetterExpired, "This letter has expired.");

            ClaimedAt = utcNow;
            ReadAt ??= utcNow;

            return Rewards;
        }
    }
}
