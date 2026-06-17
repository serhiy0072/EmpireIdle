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

        public async Task NotifyResourcesUpdatedAsync(Guid playerId, IReadOnlyDictionary<string, int> resources, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(playerId.ToString()).SendAsync("ResourcesUpdated", resources, cancellationToken);
        }
                         
        public async Task NotifyBuildingUpgradedAsync(Guid playerId, Guid buildingId, int newLevel, CancellationToken cancellationToken = default)
        {
            await _hubContext.Clients.Group(playerId.ToString()).SendAsync("BuildingUpgraded", new { buildingId, newLevel }, cancellationToken);
        }
    }
}
