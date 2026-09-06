using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Clans.EventHandlers
{
    /// <summary>
    /// Реагує на ClanInviteSent: шле гравцеві real-time сповіщення.
    /// Офлайн-гравець його не побачить — запрошення лишається в списку
    /// запрошень, і саме він, а не хаб, є джерелом істини.
    /// </summary>
    public sealed class ClanInviteSentNotificationHandler
        : INotificationHandler<DomainEventNotification<ClanInviteSent>>
    {
        private readonly IClanRepository _clanRepository;
        private readonly IGameNotifier _notifier;
        private readonly ILogger<ClanInviteSentNotificationHandler> _logger;

        public ClanInviteSentNotificationHandler(
            IClanRepository clanRepository,
            IGameNotifier notifier,
            ILogger<ClanInviteSentNotificationHandler> logger)
        {
            _clanRepository = clanRepository;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<ClanInviteSent> notification,
            CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;

            var card = await _clanRepository.GetCardAsync(e.ClanId, cancellationToken);

            if (card is null)
            {
                // Клан розпався між надсиланням і доставкою — сповіщати нема про що
                _logger.LogInformation("Clan {ClanId} is gone, invite {RequestId} not delivered",
                    e.ClanId, e.RequestId);

                return;
            }

            await _notifier.NotifyClanInviteAsync(e.PlayerId, e.RequestId, card.Id, card.Name, card.Tag,
                e.ExpiresAt, cancellationToken);
        }
    }
}
