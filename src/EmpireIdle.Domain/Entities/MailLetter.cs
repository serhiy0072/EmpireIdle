using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Особистий лист у скриньці (GDD §7.4): рядок на адресата.
    ///
    /// Не зліпок стану, а посилання: через три доби запрошення може бути вже
    /// протерміноване, а клан — розпущений. Лист лишається й після дії —
    /// історія важлива при скаргах, — але кнопки вирішує актуальний стан.
    /// </summary>
    public class MailLetter : Entity
    {
        public int ServerId { get; private set; }

        public Guid PlayerId { get; private set; }

        public MailKind Kind { get; private set; }

        /// <summary>На що посилається лист; тип сутності задає Kind.</summary>
        public Guid ReferenceId { get; private set; }

        public DateTime CreatedAt { get; private set; }

        /// <summary>Після цього моменту лист прибирає джоб.</summary>
        public DateTime ExpiresAt { get; private set; }

        public DateTime? ReadAt { get; private set; }

        public bool IsRead => ReadAt is not null;

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

        protected MailLetter() { } // Для EF Core

        /// <summary>Позначка прочитання. Повторне відкриття не переписує першого моменту.</summary>
        public void MarkRead(DateTime utcNow) => ReadAt ??= utcNow;
    }
}
