using EmpireIdle.Application.Interfaces;

namespace EmpireIdle.API.Services
{
    /// <inheritdoc/>
    public class ServerContext : IServerContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        private int? _explicitServerId;

        public ServerContext(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        /// <inheritdoc/>
        public int ServerId
        {
            get
            {
                // Явно встановлений світ має пріоритет: фонові джоби, реєстрація, Outbox
                if (_explicitServerId is { } explicitId)
                    return explicitId;

                var claim = _httpContextAccessor.HttpContext?.User.FindFirst("serverId")?.Value;

                // Fail-closed: без сервера в токені запит не має виконуватись
                return int.TryParse(claim, out var id)
                    ? id
                    : throw new UnauthorizedAccessException("Request has no server context.");
            }
        }

        /// <inheritdoc/>
        public void UseServer(int serverId) => _explicitServerId = serverId;
    }
}
