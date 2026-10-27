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

        /// <inheritdoc/>
        public IReadOnlyList<string> AcceptedLanguages
        {
            get
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                if (request is null)
                    return [];

                // q=0 — «цієї мови не треба»; OrderByDescending стабільний, тож рівні q лишаються в порядку заголовка
                return request.GetTypedHeaders().AcceptLanguage
                    .Where(l => l.Quality is not 0 && l.Value.HasValue && l.Value != "*")
                    .OrderByDescending(l => l.Quality ?? 1)
                    .Select(l => l.Value.Value!.Split('-')[0].ToLowerInvariant())
                    .Distinct()
                    .ToList();
            }
        }
    }
}
