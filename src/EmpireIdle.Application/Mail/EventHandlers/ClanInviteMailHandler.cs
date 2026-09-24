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
    /// Запрошення в клан кладе лист у скриньку адресата. Той, хто запрошує,
    /// про скриньку не знає (GDD §7.4): лист — підписник на подію, як і
    /// прогрес квестів. Real-time лише підсвічує новий лист.
    /// </summary>
    public sealed class ClanInviteMailHandler : INotificationHandler<DomainEventNotification<ClanInviteSent>>
    {
        private readonly IMailRepository _mail;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameNotifier _notifier;
        private readonly GameCatalog _catalog;

        public ClanInviteMailHandler(IMailRepository mail, IServerContext serverContext, IUnitOfWork unitOfWork,
            IGameNotifier notifier, GameCatalog catalog)
        {
            _mail = mail;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _catalog = catalog;
        }

        public async Task Handle(DomainEventNotification<ClanInviteSent> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var letter = new MailLetter(Guid.NewGuid(), _serverContext.ServerId, e.PlayerId, MailKind.ClanInvite, e.RequestId,
                e.OccurredAt, TimeSpan.FromDays(_catalog.Config.Mail.LetterRetentionDays));

            await _mail.AddLetterAsync(letter, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyMailAsync(e.PlayerId, MailKind.ClanInvite.ToString(), cancellationToken);
        }
    }
}
