using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EmpireIdle.API.Hubs
{
    /// <summary>
    /// SignalR hub для real-time оновлень гри.
    /// Кожен гравець у своїй групі (group name = playerId) для адресних сповіщень
    /// і в групі свого світу — для серверного чату.
    /// </summary>
    [Authorize]
    public class GameHub : Hub<IGameClient>
    {
        private readonly ILogger<GameHub> _logger;

        public GameHub(ILogger<GameHub> logger) => _logger = logger;

        /// <summary>
        /// Приєднує з'єднання до групи гравця автоматично при підключенні.
        /// PlayerId береться з токена — клієнт не може підписатись на чужі події.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var playerId = GetPlayerId();

            if (playerId is null)
            {
                _logger.LogWarning("Connection {ConnectionId} has no playerId claim.", Context.ConnectionId);
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, playerId);

            // Світ — із токена, як і playerId: гравець бачить лише свій серверний чат
            if (GetServerId() is { } serverId)
                await Groups.AddToGroupAsync(Context.ConnectionId, ServerGroup(serverId));

            await base.OnConnectedAsync();
        }

        /// <summary>Прибирає з'єднання з групи при відключенні.</summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var playerId = GetPlayerId();

            if (playerId is not null)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerId);

            if (GetServerId() is { } serverId)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, ServerGroup(serverId));

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>Група світу. Префікс не дає їй збігтися з групою гравця.</summary>
        public static string ServerGroup(int serverId) => $"server:{serverId}";

        /// <summary>PlayerId із claims поточного з'єднання.</summary>
        private string? GetPlayerId() => Context.User?.FindFirst("playerId")?.Value;

        private int? GetServerId()
            => int.TryParse(Context.User?.FindFirst("serverId")?.Value, out var serverId) ? serverId : null;
    }
}
