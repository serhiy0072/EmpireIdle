using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using EmpireIdle.API.DTOs;

namespace EmpireIdle.Api.Tests.Auth;

/// <summary>
/// Ротація refresh-токена в одній транзакції: новий токен видається разом із відкликанням
/// старого, а повторне використання старого — ознака крадіжки — відкликає всі сесії,
/// і це відкликання мусить закомітитись, а не відкотитись разом із транзакцією.
/// </summary>
public class RefreshRotationTests : IClassFixture<RegistrationFixture>
{
    private readonly HttpClient _client;

    public RefreshRotationTests(RegistrationFixture fixture) => _client = fixture.Client;

    private async Task<AuthResponse> RegisterAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"r{suffix}", $"r{suffix}@test.local", "Password123"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private Task<HttpResponseMessage> RefreshAsync(string token)
        => _client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(token));

    [Fact]
    public async Task Refresh_ShouldRotate_AndRevokeEverything_OnReuse()
    {
        var first = await RegisterAsync();

        var rotated = await RefreshAsync(first.RefreshToken);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<AuthResponse>())!;
        second.RefreshToken.Should().NotBe(first.RefreshToken);

        // Старий токен удруге — крадіжка: відмова, і відкликання всіх сесій закомічене
        (await RefreshAsync(first.RefreshToken)).StatusCode.Should().NotBe(HttpStatusCode.OK);
        (await RefreshAsync(second.RefreshToken)).StatusCode.Should().NotBe(HttpStatusCode.OK);
    }
}
