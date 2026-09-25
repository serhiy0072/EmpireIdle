using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Territory.EventHandlers
{
    /// <summary>Споруду зруйновано — клан бачить це одразу, а не при наступному відкритті карти.</summary>
    public sealed class ClanStructureDestroyedHandler : INotificationHandler<DomainEventNotification<ClanStructureDestroyed>>
    {
        private readonly IPlayerRepository _players;
        private readonly IGameNotifier _notifier;

        public ClanStructureDestroyedHandler(IPlayerRepository players, IGameNotifier notifier)
        {
            _players = players;
            _notifier = notifier;
        }

        public async Task Handle(DomainEventNotification<ClanStructureDestroyed> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var members = await _players.GetIdsByClanAsync(e.ClanId, cancellationToken);

            await _notifier.NotifyStructureDestroyedAsync(members, e.StructureId, e.X, e.Y, cancellationToken);
        }
    }
}
