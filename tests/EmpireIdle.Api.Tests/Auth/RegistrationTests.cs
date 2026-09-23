using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.API.DTOs;
using Testcontainers.PostgreSql;

namespace EmpireIdle.Api.Tests.Auth;

/// <summary>Контейнер і застосунок — на весь клас: реєстрації впираються в лімітер "auth".</summary>
public class RegistrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new TestApiFactory(_container.GetConnectionString());
        await Factory.MigrateAsync();

        Client = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Відмова в реєстрації — перше, що може побачити новачок. Вона має нести
/// причину з кодами Identity, а не англійський Detail: сторінка реєстрації
/// перекладає кожен код і радить, що виправити.
/// </summary>
public class RegistrationTests : IClassFixture<RegistrationFixture>
{
    private readonly HttpClient _client;

    public RegistrationTests(RegistrationFixture fixture) => _client = fixture.Client;

    private Task<HttpResponseMessage> RegisterAsync(string userName, string email)
        => _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(userName, email, "Password123"));

    private static async Task<(string? Reason, string Codes)> RefusalAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        return (root.GetProperty("reason").GetString(), root.GetProperty("args").GetProperty("codes").GetString()!);
    }

    [Fact]
    public async Task Register_WithATakenEmail_ShouldExplainWithTheIdentityCode()
    {
        var email = $"dup-{Guid.NewGuid():N}@test.local";
        (await RegisterAsync($"a{Guid.NewGuid():N}"[..12], email)).StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await RegisterAsync($"b{Guid.NewGuid():N}"[..12], email);

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var (reason, codes) = await RefusalAsync(second);
        reason.Should().Be("auth.registrationRejected");
        codes.Split(',').Should().Contain("DuplicateEmail");
    }

    /// <summary>
    /// Фіксує поточне правило імені з RegisterRequest: 3–50 символів, лише латиниця,
    /// цифри й «._-». Клієнт дзеркалить його до відправки. Якщо дозволимо
    /// кирилицю, цей тест має змінитися разом із правилом на обох боках.
    /// </summary>
    [Fact]
    public async Task Register_WithACyrillicName_ShouldBeRejectedByTheNameRule()
    {
        var response = await RegisterAsync("Сергій", $"cyr-{Guid.NewGuid():N}@test.local");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").TryGetProperty("UserName", out _).Should().BeTrue();
    }
}
