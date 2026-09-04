using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.DTOs
{
    // Атрибути на параметрах, не [property:]: MVC валідує record
    // через параметри конструктора і кидає, якщо метадані на властивостях.
    // Межі = обмеження БД: Player.Username(50), Player.Email(200).
    public record RegisterRequest(
        [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[A-Za-z0-9._-]+$")]
        string UserName,
        [Required, EmailAddress, StringLength(200)]
        string Email,
        [Required, StringLength(128, MinimumLength = 8)]
        string Password);

    public record LoginRequest(
        [Required, EmailAddress, StringLength(200)] string Email,
        [Required, StringLength(128)] string Password);

    public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId);

    public record RefreshRequest(
        [Required, StringLength(128, MinimumLength = 64)] string RefreshToken);
}
