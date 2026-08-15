using EmpireIdle.Domain.Entities;
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

        public AuthService(UserManager<IdentityUser> userManager, AppDbContext context, IOptions<JwtSettings> jwtSettings)
        {
            _userManager = userManager;
            _context = context;
            _jwtSettings = jwtSettings.Value;
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
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Registration failed: {errors}");
            }

            return user.Id;
        }

        /// <summary>
        /// Залогінити користувача і повернути JWT + refresh token.
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken, Guid PlayerId)> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("Invalid email or password.");

            var validPassword = await _userManager.CheckPasswordAsync(user, password);
            if (!validPassword)
            {
                await _userManager.AccessFailedAsync(user);
                throw new InvalidOperationException("Invalid email or password.");
            }

            if (await _userManager.IsLockedOutAsync(user))
                throw new InvalidOperationException("Account temporarily locked. Try again later.");

            await _userManager.ResetAccessFailedCountAsync(user);

            var playerId = await GetPlayerIdAsync(user.Email!);
            var accessToken = GenerateAccessToken(user, playerId);
            var refreshToken = await CreateRefreshTokenAsync(user.Id);

            return (accessToken, refreshToken, playerId);
        }

        /// <summary>
        /// Оновити пару токенів за refresh token. Старий токен ревокується (ротація).
        /// </summary>
        public async Task<(string AccessToken, string RefreshToken, Guid PlayerId)> RefreshAsync(string refreshToken)
        {
            var storedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken)
                ?? throw new InvalidOperationException("Invalid refresh token.");

            // Спроба використати ревокнутий токен = можлива крадіжка.
            // Ревокуємо ВСІ токени користувача — змусить перелогінитись всюди.
            if (storedToken.RevokedAt is not null)
            {
                await RevokeAllUserTokensAsync(storedToken.UserId);
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Token reuse detected. All sessions revoked.");
            }

            if (!storedToken.IsActive)
                throw new InvalidOperationException("Refresh token expired.");

            var user = await _userManager.FindByIdAsync(storedToken.UserId)
                ?? throw new InvalidOperationException("User not found.");

            // Ротація: ревокуємо старий, створюємо новий
            var newRefreshToken = GenerateRefreshTokenString();
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.ReplacedByToken = newRefreshToken;

            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = newRefreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
            });

            await _context.SaveChangesAsync();

            var playerId = await GetPlayerIdAsync(user.Email!);
            var accessToken = GenerateAccessToken(user, playerId);
            return (accessToken, newRefreshToken, playerId);
        }

        private async Task<string> CreateRefreshTokenAsync(string userId)
        {
            var token = GenerateRefreshTokenString();

            _context.RefreshTokens.Add(new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
            });

            await _context.SaveChangesAsync();
            return token;
        }

        private async Task RevokeAllUserTokensAsync(string userId)
        {
            var tokens = await _context.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null).ToListAsync();

            foreach (var token in tokens)
                token.RevokedAt = DateTime.UtcNow;
        }

        private string GenerateAccessToken(IdentityUser user, Guid playerId)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("playerId", playerId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
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

        /// <summary>Знаходить доменного гравця за email (міст Identity ↔ Domain).</summary>
        private async Task<Guid> GetPlayerIdAsync(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();

            var player = await _context.Players.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Email == normalized)
                ?? throw new InvalidOperationException("Player not found for this account.");

            return player.Id;
        }
    }
}
