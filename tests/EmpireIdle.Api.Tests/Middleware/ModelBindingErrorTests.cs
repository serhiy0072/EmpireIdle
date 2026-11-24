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
public class ModelBindingErrorTests
{
    private readonly TestApiFactory _factory;

    public ModelBindingErrorTests(PostgresFixture postgres) => _factory = postgres.Factory;

    [Fact]
    public async Task MalformedBody_ShouldCarryTheValidationErrorCode()
    {
        using var client = _factory.CreateClient();

        // Email числом — JSON не зводиться до LoginRequest саме через тип поля, а не через відсутні поля
        using var body = new StringContent("""{"email": 5, "password": "x"}""", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/auth/login", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Validation", document.RootElement.GetProperty("errorCode").GetString());
    }
}
