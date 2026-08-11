
namespace EmpireIdle.Infrastructure.Auth
{
    /// <summary>
    /// Налаштування JWT токенів. Завантажується з appsettings/User Secrets.
    /// </summary>
    public class JwtSettings
    {
        public string Secret { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenExpirationMinutes { get; set; } = 60;
        public int RefreshTokenExpirationDays { get; set; } = 7;
    }
}
