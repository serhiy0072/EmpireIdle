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
    /// Зруйнована споруда кладе лист кожному учаснику клану: realtime-тривогу бачив лише той,
    /// хто був у грі, а слот і бонус території зникли для всіх. Лист посилається на запис падіння.
    /// </summary>
    public sealed class StructureFallMailHandler : INotificationHandler<DomainEventNotification<ClanStructureFell>>
    {
        private readonly IMailRepository _mail;
        private readonly IPlayerRepository _players;
        private readonly IServerContext _serverContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameNotifier _notifier;
        private readonly GameCatalog _catalog;

        public StructureFallMailHandler(IMailRepository mail, IPlayerRepository players, IServerContext serverContext,
            IUnitOfWork unitOfWork, IGameNotifier notifier, GameCatalog catalog)
        {
            _mail = mail;
            _players = players;
            _serverContext = serverContext;
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _catalog = catalog;
        }

        public async Task Handle(DomainEventNotification<ClanStructureFell> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var members = await _players.GetIdsByClanAsync(e.ClanId, cancellationToken);
            var retention = TimeSpan.FromDays(_catalog.Config.Mail.LetterRetentionDays);

            foreach (var playerId in members)
            {
                await _mail.AddLetterAsync(new MailLetter(Guid.NewGuid(), _serverContext.ServerId, playerId,
                    MailKind.StructureFall, e.FallId, e.OccurredAt, retention), cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var playerId in members)
                await _notifier.NotifyMailAsync(playerId, MailKind.StructureFall.ToString(), cancellationToken);
        }
    }
}
