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
    /// Кожен звіт розвідки лягає листом у скриньку: над нею світиться «непрочитане», і звіт
    /// не загубиться, якщо гравець був не на мапі. Лист посилається на звіт, а не копіює його.
    /// </summary>
    public sealed class ScoutReportMailHandler : INotificationHandler<DomainEventNotification<ScoutReportFiled>>
    {
        private readonly IMailRepository _mail;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameNotifier _notifier;
        private readonly GameCatalog _catalog;

        public ScoutReportMailHandler(IMailRepository mail, IServerContext serverContext, IUnitOfWork unitOfWork,
            IGameNotifier notifier, GameCatalog catalog)
        {
            _mail = mail;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _catalog = catalog;
        }

        public async Task Handle(DomainEventNotification<ScoutReportFiled> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var letter = new MailLetter(Guid.NewGuid(), _serverContext.ServerId, e.PlayerId, MailKind.ScoutReport, e.ReportId,
                e.OccurredAt, TimeSpan.FromDays(_catalog.Config.Mail.LetterRetentionDays));

            await _mail.AddLetterAsync(letter, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyMailAsync(e.PlayerId, MailKind.ScoutReport.ToString(), cancellationToken);
        }
    }
}
