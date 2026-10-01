using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.EventHandlers
{
    /// <summary>
    /// Ворожий марш вирушив — тривога захисникам (див. DefenderAudience). Іде з outbox
    /// після коміту, тож марш уже в базі й читається тією самою проєкцією, що й запит загроз.
    /// </summary>
    public sealed class HostileMarchLaunchedHandler : INotificationHandler<DomainEventNotification<HostileMarchLaunched>>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly DefenderAudience _audience;
        private readonly IGameNotifier _notifier;
        private readonly ILogger<HostileMarchLaunchedHandler> _logger;

        public HostileMarchLaunchedHandler(
            IMarchRepository marchRepository,
            DefenderAudience audience,
            IGameNotifier notifier,
            ILogger<HostileMarchLaunchedHandler> logger)
        {
            _marchRepository = marchRepository;
            _audience = audience;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<HostileMarchLaunched> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var attack = await _marchRepository.GetIncomingAttackAsync(e.MarchId, cancellationToken);

            // Поки outbox дійшов, марш міг уже прибути чи розвернутись — тривожити пізно
            if (attack is null)
            {
                _logger.LogInformation("Hostile march {MarchId} is no longer on its way; no alert sent.", e.MarchId);
                return;
            }

            var recipients = await _audience.ResolveAsync(e.TargetType, e.TargetId, cancellationToken);

            if (recipients.Count > 0)
                await _notifier.NotifyAttackIncomingAsync(recipients, attack, cancellationToken);
        }
    }
}
