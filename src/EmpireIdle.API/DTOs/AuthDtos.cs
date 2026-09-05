using System.ComponentModel.DataAnnotations;

namespace EmpireIdle.API.DTOs;

/// <summary>Реєстрація: створює користувача, гравця й одразу видає токени.</summary>
public record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[A-Za-z0-9._-]+$")]
        string UserName,
    [Required, EmailAddress, StringLength(200)]
        string Email,
    [Required, StringLength(128, MinimumLength = 8)]
        string Password);

/// <summary>Вхід за поштою та паролем.</summary>
public record LoginRequest(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(128)] string Password);

/// <summary>Пара токенів і гравець, до якого вони прив'язані.</summary>
public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId);

/// <summary>Обмін refresh-токена на нову пару. Старий одразу ревокується.</summary>
public record RefreshRequest(
    [Required, StringLength(128, MinimumLength = 64)] string RefreshToken);
