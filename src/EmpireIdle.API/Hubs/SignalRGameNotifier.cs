using EmpireIdle.API.Hubs.Events;
using EmpireIdle.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace EmpireIdle.API.Hubs
{
    /// <summary>
    /// Реалізація IGameNotifier через SignalR. Пушить події в групу гравця.
    ///
    /// Group, а не User: UserIdentifier у SignalR — це IdentityUser.Id (sub), а не playerId.
    /// cancellationToken не передається далі: подія летить після вже закоміченої
    /// транзакції, скасовувати її нема сенсу.
    /// </summary>
    public class SignalRGameNotifier : IGameNotifier
    {
        private readonly IHubContext<GameHub, IGameClient> _hubContext;

        public SignalRGameNotifier(IHubContext<GameHub, IGameClient> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <inheritdoc/>
        public Task NotifyBuildingCollectedAsync(Guid playerId, Guid buildingId, string resourceType, int collected,
            int newVillageAmount, CancellationToken cancellationToken = default)
            => Player(playerId).BuildingCollected(
                new BuildingCollectedEvent(buildingId, resourceType, collected, newVillageAmount));

        /// <inheritdoc/>
        public Task NotifyUpgradeStartedAsync(Guid playerId, Guid buildingId, DateTime completesAt,
            CancellationToken cancellationToken = default)
            => Player(playerId).UpgradeStarted(new UpgradeStartedEvent(buildingId, completesAt));

        /// <inheritdoc/>
        public Task NotifyUpgradeCompletedAsync(Guid playerId, Guid buildingId, int newLevel,
            CancellationToken cancellationToken = default)
            => Player(playerId).UpgradeCompleted(new UpgradeCompletedEvent(buildingId, newLevel));

        /// <inheritdoc/>
        public Task NotifyBattleFinishedAsync(Guid playerId, Guid reportId, bool won, string targetName,
            CancellationToken cancellationToken = default)
            => Player(playerId).BattleFinished(new BattleFinishedEvent(reportId, won, targetName));

        /// <inheritdoc/>
        public Task NotifyServerQuestRewardedAsync(Guid playerId, string questKey, int rank, long contribution,
            CancellationToken cancellationToken = default)
            => Player(playerId).ServerQuestRewarded(new ServerQuestRewardedEvent(questKey, rank, contribution));

        /// <inheritdoc/>
        public Task NotifyMarchReturnedAsync(Guid playerId, Guid marchId, CancellationToken cancellationToken = default)
            => Player(playerId).MarchReturned(new MarchReturnedEvent(marchId));

        /// <inheritdoc/>
        public Task NotifyClanInviteAsync(Guid playerId, Guid requestId, Guid clanId, string clanName, string clanTag,
            DateTime expiresAt, CancellationToken cancellationToken = default)
            => Player(playerId).ClanInvite(new ClanInviteEvent(requestId, clanId, clanName, clanTag, expiresAt));

        private IGameClient Player(Guid playerId) => _hubContext.Clients.Group(playerId.ToString());
    }
}
