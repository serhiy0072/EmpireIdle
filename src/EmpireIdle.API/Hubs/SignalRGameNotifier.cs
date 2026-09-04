using EmpireIdle.Application.Interfaces;
using EmpireIdle.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EmpireIdle.API.Hubs
{
    /// <summary>
    /// Реалізація IGameNotifier через SignalR.
    /// Пушить події в групу конкретного гравця.
    /// </summary>
    public class SignalRGameNotifier : IGameNotifier
    {
        private readonly IHubContext<GameHub> _hubContext;
        public SignalRGameNotifier(IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <inheritdoc/>
        public async Task NotifyBuildingCollectedAsync(Guid playerId, Guid buildingId, string resourceType, int collected, int newVillageAmount, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(playerId.ToString())
                .SendAsync("BuildingCollected", new { buildingId, resourceType, collected, newVillageAmount }, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task NotifyUpgradeStartedAsync(Guid playerId, Guid buildingId, DateTime completesAt, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(playerId.ToString()).SendAsync("UpgradeStarted", new {buildingId, completesAt}, cancellationToken);
        }

        /// <inheritdoc/>        
        public async Task NotifyUpgradeCompletedAsync(Guid playerId, Guid buildingId, int newLevel, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(playerId.ToString()).SendAsync("UpgradeCompleted", new { buildingId, newLevel }, cancellationToken);
        }

        /// <inheritdoc/>
        public Task NotifyBattleFinishedAsync(Guid playerId, Guid reportId, bool won, string targetName, CancellationToken cancellationToken = default)
            // Group, не User: UserIdentifier у SignalR — це IdentityUser.Id (sub), а не playerId
            => _hubContext.Clients.Group(playerId.ToString())
                .SendAsync("BattleFinished", new { reportId, won, targetName }, cancellationToken);

        /// <inheritdoc/>
        public Task NotifyServerQuestRewardedAsync(Guid playerId, string questKey, int rank, long contribution,
            CancellationToken cancellationToken = default)
            => _hubContext.Clients.Group(playerId.ToString())
                .SendAsync("ServerQuestRewarded", new { questKey, rank, contribution }, cancellationToken);
    }
}
