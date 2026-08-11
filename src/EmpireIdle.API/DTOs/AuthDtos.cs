namespace EmpireIdle.API.DTOs
{
    public record RegisterRequest(string UserName, string Email, string Password);

    public record LoginRequest(string Email, string Password);

    public record AuthResponse(string AccessToken, string RefreshToken, Guid PlayerId);

    public record RefreshRequest(string RefreshToken);
}
