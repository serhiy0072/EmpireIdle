using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Mail.EventHandlers
{
    /// <summary>
    /// Падіння міста кладе лист виселеному (GDD §2.6, §7.4): гравець,
    /// що повернувся в гру, має дізнатись, чому він в іншому місці.
    /// Лист посилається на запис падіння, а не копіює його.
    /// </summary>
    public sealed class CityFallMailHandler : INotificationHandler<DomainEventNotification<VillageFell>>
    {
        private readonly IMailRepository _mail;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameNotifier _notifier;
        private readonly GameCatalog _catalog;

        public CityFallMailHandler(IMailRepository mail, IServerContext serverContext, IUnitOfWork unitOfWork,
            IGameNotifier notifier, GameCatalog catalog)
        {
            _mail = mail;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _catalog = catalog;
        }

        public async Task Handle(DomainEventNotification<VillageFell> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var letter = new MailLetter(Guid.NewGuid(), _serverContext.ServerId, e.PlayerId, MailKind.CityFall, e.FallId,
                e.OccurredAt, TimeSpan.FromDays(_catalog.Config.Mail.LetterRetentionDays));

            await _mail.AddLetterAsync(letter, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyMailAsync(e.PlayerId, MailKind.CityFall.ToString(), cancellationToken);
        }
    }
}
