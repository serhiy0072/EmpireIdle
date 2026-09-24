using System.Net;
using System.Text;
using System.Text.Json;
using EmpireIdle.Api.Tests.Infrastructure;

namespace EmpireIdle.Api.Tests.Middleware;

/// <summary>
/// Помилка прив'язки моделі (кривий JSON, невідомий enum) відсікається до
/// FluentValidation й глобального обробника. Вона теж мусить нести errorCode —
/// інакше клієнт не має за чим розгалужуватись.
/// </summary>
[Collection("postgres")]
public class ModelBindingErrorTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ModelBindingErrorTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task MalformedBody_ShouldCarryTheValidationErrorCode()
    {
        using var client = _factory.CreateClient();

        // Ім'я користувача числом — JSON не зводиться до LoginRequest
        using var body = new StringContent("""{"userName": 5}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Validation", document.RootElement.GetProperty("errorCode").GetString());
    }
}
