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
    /// Прискорений марш тривожить удруге тією самою подією з новим часом: клієнт замінює
    /// тривогу за ключем маршу й перечитує загрози.
    /// </summary>
    public sealed class HostileMarchLaunchedHandler
        : INotificationHandler<DomainEventNotification<HostileMarchLaunched>>,
          INotificationHandler<DomainEventNotification<HostileMarchRetimed>>
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

        public Task Handle(DomainEventNotification<HostileMarchLaunched> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return AlertAsync(e.MarchId, e.TargetType, e.TargetId, cancellationToken);
        }

        public Task Handle(DomainEventNotification<HostileMarchRetimed> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            return AlertAsync(e.MarchId, e.TargetType, e.TargetId, cancellationToken);
        }

        private async Task AlertAsync(Guid marchId, Domain.Enums.MarchTargetType targetType, Guid targetId,
            CancellationToken cancellationToken)
        {
            var attack = await _marchRepository.GetIncomingAttackAsync(marchId, cancellationToken);

            // Поки outbox дійшов, марш міг уже прибути чи розвернутись — тривожити пізно
            if (attack is null)
            {
                _logger.LogInformation("Hostile march {MarchId} is no longer on its way; no alert sent.", marchId);
                return;
            }

            var recipients = await _audience.ResolveAsync(targetType, targetId, cancellationToken);

            if (recipients.Count > 0)
                await _notifier.NotifyAttackIncomingAsync(recipients, attack, cancellationToken);
        }
    }
}
