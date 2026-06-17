using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EmpireIdle.Infrastructure.Auth
{
    /// <summary>
    /// Сервіс аутентифікації: реєстрація, логін, генерація JWT.
    /// </summary>
    public class AuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JwtSettings _jwtSettings;

        public AuthService(UserManager<IdentityUser> userManager, JwtSettings jwtSettings)
        {
            _userManager = userManager;
            _jwtSettings = jwtSettings;
        }

        /// <summary>
        /// Зареєструвати нового Identity користувача.
        /// </summary>
        /// <returns>IdentityUser.Id</returns>
        public async Task<string> RegisterAsync(string username, string email, string passeord)
        {
            var user = new IdentityUser
            {
                UserName = username,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, passeord);

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
        public async Task<(string  AccessTocken, string RefreshToken)> LoginAsync(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email) 
                ?? throw new InvalidOperationException("Invalid email or password.");

            var validPassword = await _userManager.CheckPasswordAsync(user, password);
            if(!validPassword)
                throw new InvalidOperationException("Invalid email or password.");

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            return (accessToken, refreshToken);
        }

        private string GenerateAccessToken(IdentityUser user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenInspirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
