using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EmpireIdle.API.Hubs
{
    /// <summary>
    /// SignalR hub для real-time оновлень гри.
    /// Кожен гравець у своїй групі (group name = playerId) для адресних сповіщень.
    /// </summary>
    [Authorize]
    public class GameHub : Hub
    {
        /// <summary>
        /// Гравець приєднується до своєї групи для отримання персональних оновлень.
        /// </summary>
        public async Task JoinPlayerGroupAsync(string playerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, playerId);
        }

        /// <summary>
        /// Гравець залишає свою групу.
        /// </summary>
        public async Task LeavePlayerGroupAsync(string playerId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerId);
        }
    }
}
