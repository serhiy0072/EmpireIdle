using EmpireIdle.Application.Interfaces;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

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

        /// <inheritdoc/>
        public string? UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                // JwtBearer за замовчуванням перемаповує `sub` у NameIdentifier
                return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            }
        }
    }
}
