using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Marches.EventHandlers
{
    /// <summary>
    /// Ворожий марш вирушив — тривога захисникам: усьому клану цілі, а якщо
    /// власник села поза кланом — йому самому. Іде з outbox після коміту,
    /// тож марш уже в базі й читається тією самою проєкцією, що й запит загроз.
    /// </summary>
    public sealed class HostileMarchLaunchedHandler : INotificationHandler<DomainEventNotification<HostileMarchLaunched>>
    {
        private readonly IMarchRepository _marchRepository;
        private readonly IPlayerRepository _playerRepository;
        private readonly IGameNotifier _notifier;
        private readonly ILogger<HostileMarchLaunchedHandler> _logger;

        public HostileMarchLaunchedHandler(
            IMarchRepository marchRepository,
            IPlayerRepository playerRepository,
            IGameNotifier notifier,
            ILogger<HostileMarchLaunchedHandler> logger)
        {
            _marchRepository = marchRepository;
            _playerRepository = playerRepository;
            _notifier = notifier;
            _logger = logger;
        }

        public async Task Handle(DomainEventNotification<HostileMarchLaunched> notification, CancellationToken cancellationToken)
        {
            var e = notification.DomainEvent;
            var attack = await _marchRepository.GetIncomingAttackAsync(e.MarchId, cancellationToken);

            // Поки outbox дійшов, марш міг уже прибути — тривожити пізно
            if (attack is null)
            {
                _logger.LogInformation("Hostile march {MarchId} is no longer on its way; no alert sent.", e.MarchId);
                return;
            }

            IReadOnlyCollection<Guid> recipients = attack.DefenderClanId is { } clanId
                ? await _playerRepository.GetIdsByClanAsync(clanId, cancellationToken)
                : attack.TargetOwnerId is { } ownerId ? [ownerId] : [];

            if (recipients.Count == 0)
                return;

            await _notifier.NotifyAttackIncomingAsync(recipients, attack, cancellationToken);
        }
    }
}
