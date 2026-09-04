using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.DTOs
{
    // Межі = обмеження БД: Player.Username(50), Player.Email(200).
    // Символи — підмножина Identity AllowedUserNameCharacters.
    public record RegisterRequest(
        [property: Required, StringLength(50, MinimumLength = 3), RegularExpression("^[A-Za-z0-9._-]+$")]
        string UserName,
        [property: Required, EmailAddress, StringLength(200)]
        string Email,
        [property: Required, StringLength(128, MinimumLength = 8)]
        string Password);

    public record LoginRequest(
        [property: Required, EmailAddress, StringLength(200)] string Email,
        [property: Required, StringLength(128)] string Password);

    public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId);

    public record RefreshRequest(
        [property: Required, StringLength(128, MinimumLength = 64)] string RefreshToken);
}
