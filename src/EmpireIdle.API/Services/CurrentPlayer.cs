using EmpireIdle.Application.Interfaces;

namespace EmpireIdle.API.Services
{
    /// <summary>Читає playerId з claims поточного HTTP-запиту.</summary>
    public class CurrentPlayer : ICurrentPlayer
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentPlayer(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public Guid? PlayerId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("playerId")?.Value;
                return Guid.TryParse(claim, out var id) ? id : null;
            }
        }
    }
}
