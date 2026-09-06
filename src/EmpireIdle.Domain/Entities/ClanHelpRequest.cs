using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Entities
{
    /// <summary>
    /// Запит на клановy допомогу з таймером.
    ///
    /// Створює гравець вручну — автоматичний запит на кожен апгрейд
    /// засмітив би список клану двадцятьма рядками з одного села.
    /// </summary>
    public class ClanHelpRequest : Entity
    {
        private readonly List<ClanHelpContribution> _helpers = new();

        public int ServerId { get; private set; }

        public Guid ClanId { get; private set; }

        /// <summary>Хто просить.</summary>
        public Guid PlayerId { get; private set; }

        public ClanHelpTarget TargetType { get; private set; }

        /// <summary>Id будівлі або замовлення тренування.</summary>
        public Guid TargetId { get; private set; }

        /// <summary>
        /// Повна тривалість таймера на момент запиту. Частка рахується від неї,
        /// а не від залишку: інакше кожен наступний клік давав би менше,
        /// і двадцять допомог ніколи не склали б обіцяних 40%.
        /// </summary>
        public TimeSpan FullDuration { get; private set; }

        /// <summary>Коли запит перестає бути актуальним — таймер завершиться сам.</summary>
        public DateTime ExpiresAt { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public IReadOnlyCollection<ClanHelpContribution> Helpers => _helpers.AsReadOnly();

        /// <summary>Скільки допомог уже прийнято.</summary>
        public int HelpCount => _helpers.Count;

        public ClanHelpRequest(Guid id, int serverId, Guid clanId, Guid playerId,
            ClanHelpTarget targetType, Guid targetId, TimeSpan fullDuration, DateTime expiresAt,
            DateTime utcNow) : base(id)
        {
            ServerId = serverId;
            ClanId = clanId;
            PlayerId = playerId;
            TargetType = targetType;
            TargetId = targetId;
            FullDuration = fullDuration;
            ExpiresAt = expiresAt;
            CreatedAt = utcNow;
        }

        protected ClanHelpRequest() { } // для EF Core

        /// <summary>
        /// Приймає допомогу й повертає, скільки часу зрізано.
        ///
        /// Один гравець допомагає один раз — це також унікальний індекс
        /// у базі: перевірку тут можна забути при рефакторингу, індекс ні.
        /// </summary>
        /// <param name="maxHelpers">Скільки допомог приймає запит (кап ÷ частка).</param>
        public TimeSpan AcceptHelp(Guid helperId, double sharePerHelp, int maxHelpers, DateTime utcNow)
        {
            if (helperId == PlayerId)
                throw new RequirementNotMetException("You cannot help yourself.");

            if (utcNow >= ExpiresAt)
                throw new InvalidStateException("The request has expired.");

            if (_helpers.Any(h => h.HelperId == helperId))
                throw new AlreadyExistsException("Clan help", helperId.ToString());

            if (_helpers.Count >= maxHelpers)
                throw new InvalidStateException($"The request already received all {maxHelpers} helps.");

            _helpers.Add(new ClanHelpContribution(Guid.NewGuid(), Id, helperId, utcNow));

            return FullDuration * sharePerHelp;
        }
    }
}
