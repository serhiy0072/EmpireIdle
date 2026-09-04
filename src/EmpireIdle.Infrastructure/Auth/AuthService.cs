using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EmpireIdle.Infrastructure.Auth
{
    /// <summary>
    /// Сервіс аутентифікації: реєстрація, логін, генерація JWT + refresh token rotation..
    /// </summary>
    public class AuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly TimeProvider _timeProvider;

        public AuthService(UserManager<IdentityUser> userManager, AppDbContext context, IOptions<JwtSettings> jwtSettings, TimeProvider timeProvider)
        {
            _userManager = userManager;
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Зареєструвати нового Identity користувача.
        /// </summary>
        /// <returns>IdentityUser.Id</returns>
        public async Task<string> RegisterAsync(string username, string email, string password)
        {
            var user = new IdentityUser
            {
                UserName = username,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                // Коди, не описи: "DuplicateEmail" не видає, що email зареєстрований
                var codes = string.Join("; ", result.Errors.Select(e => e.Code));
                throw new RequirementNotMetException($"Registration failed: {codes}");
            }

            return user.Id;
        }

        /// <summary>
        /// Залогінити користувача і повернути JWT + refresh token.
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken, Guid PlayerId)> LoginAsync(string email, string password)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var user = await _userManager.FindByEmailAsync(email)
                ?? throw new AuthenticationFailedException("Invalid email or password.");

            if (await _userManager.IsLockedOutAsync(user))
                throw new AuthenticationFailedException("Account temporarily locked. Try again later.");

            var validPassword = await _userManager.CheckPasswordAsync(user, password);
            if (!validPassword)
            {
                await _userManager.AccessFailedAsync(user);
                throw new AuthenticationFailedException("Invalid email or password.");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var (playerId, serverId) = await GetPlayerAsync(user.Id);
            var accessToken = GenerateAccessToken(user, playerId, serverId, now);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return (accessToken, refreshToken, playerId);
        }

        /// <summary>
        /// Оновити пару токенів за refresh token. Старий токен ревокується (ротація).
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken, Guid PlayerId)> RefreshAsync(string refreshToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var hash = HashToken(refreshToken);

            var storedToken = await _context.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(rt => rt.Token == hash)
                ?? throw new AuthenticationFailedException("Invalid refresh token.");

            // Спроба використати ревокнутий токен = можлива крадіжка — ревокуємо всі
            if (storedToken.RevokedAt is not null)
            {
                await RevokeAllUserTokensAsync(storedToken.UserId, now);
                await _context.SaveChangesAsync();
                throw new AuthenticationFailedException("Token reuse detected. All sessions revoked.");
            }

            if (now >= storedToken.ExpiresAt)
                throw new AuthenticationFailedException("Refresh token expired.");

            var newRefreshToken = GenerateRefreshTokenString();
            var newHash = HashToken(newRefreshToken);

            // Атомарна ротація: паралельний запит із тим самим токеном отримає 0 рядків
            var revoked = await _context.RefreshTokens
                .Where(rt => rt.Id == storedToken.Id && rt.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(rt => rt.RevokedAt, now)
                    .SetProperty(rt => rt.ReplacedByToken, newHash));

            if (revoked == 0)
            {
                await RevokeAllUserTokensAsync(storedToken.UserId, now);
                await _context.SaveChangesAsync();
                throw new AuthenticationFailedException("Token reuse detected. All sessions revoked.");
            }

            var user = await _userManager.FindByIdAsync(storedToken.UserId)
                ?? throw new InvalidOperationException("User not found.");

            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newHash,
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpirationDays)
            });

            await _context.SaveChangesAsync();

            var (playerId, serverId) = await GetPlayerAsync(user.Id);
            var accessToken = GenerateAccessToken(user, playerId, serverId, now);
            return (accessToken, newRefreshToken, playerId);
        }

        /// <summary>У БД лежить лише хеш: дамп таблиці не дає живих сесій.</summary>
        private static string HashToken(string token)
            => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private async Task<string> CreateRefreshTokenAsync(string userId)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var token = GenerateRefreshTokenString();

            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = HashToken(token),
                CreatedAt = now,
                ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenExpirationDays)
            });

            await _context.SaveChangesAsync();
            return token;
        }

        private async Task RevokeAllUserTokensAsync(string userId, DateTime utcNow)
        {
            var tokens = await _context.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null).ToListAsync();

            foreach (var token in tokens)
                token.RevokedAt = utcNow;
        }

        private string GenerateAccessToken(IdentityUser user, Guid playerId, int serverId, DateTime utcNow)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("playerId", playerId.ToString()),
                new Claim("serverId", serverId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: utcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshTokenString()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>
        /// Гравець акаунта. Поки сервер один — беремо єдиного;
        /// коли з'являться кілька, тут буде вибір сервера з UI.
        /// </summary>
        private async Task<(Guid PlayerId, int ServerId)> GetPlayerAsync(string userId)
        {
            var players = await _context.Players
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.ServerId)
                .Select(p => new { p.Id, p.ServerId })
                .ToListAsync();

            if (players.Count == 0)
                throw new InvalidOperationException($"No player found for account {userId}.");

            var player = players[0];
            return (player.Id, player.ServerId);
        }
    }
}
