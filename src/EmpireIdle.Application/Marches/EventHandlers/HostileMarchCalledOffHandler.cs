using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Events;
using MediatR;

namespace EmpireIdle.Application.Marches.EventHandlers
{
    /// <summary>Ворожий марш розвернувся в дорозі — тим самим захисникам знімаємо тривогу.</summary>
    public sealed class HostileMarchCalledOffHandler : INotificationHandler<DomainEventNotification<HostileMarchCalledOff>>
    {
        private readonly DefenderAudience _audience;
        private readonly IGameNotifier _notifier;

        public HostileMarchCalledOffHandler(DefenderAudience audience, IGameNotifier notifier)
        {
            _audience = audience;
            _notifier = notifier;
        }

        public async Task Handle(DomainEventNotification<HostileMarchCalledOff> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var recipients = await _audience.ResolveAsync(e.TargetType, e.TargetId, cancellationToken);

            if (recipients.Count > 0)
                await _notifier.NotifyAttackCalledOffAsync(recipients, e.MarchId, cancellationToken);
        }
    }
}
