using EmpireIdle.Application.Interfaces;

namespace EmpireIdle.API.Services
{
    /// <summary>Читає метадані з поточного HTTP-запиту.</summary>
    public class RequestContext : IRequestContext
    {
        private const string IdempotencyHeader = "Idempotency-Key";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public RequestContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc/>
        public string? IdempotencyKey
        {
            get
            {
                var value = _httpContextAccessor.HttpContext?.Request.Headers[IdempotencyHeader].FirstOrDefault();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
        }
    }
}