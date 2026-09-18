using System.Text.Json;
using EmpireIdle.Api.Tests.Infrastructure;

namespace EmpireIdle.Api.Tests.Contract;

/// <summary>
/// Схема відбиває анотації nullable з C#. Інакше генератор типів робить
/// кожне поле необов'язковим, і фронт обробляє undefined там, де його не буває.
/// </summary>
[Collection("postgres")]
public class SchemaNullabilityTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public SchemaNullabilityTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private async Task<JsonElement> SchemasAsync()
    {
        var json = await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        return JsonDocument.Parse(json).RootElement.GetProperty("components").GetProperty("schemas").Clone();
    }

    [Fact]
    public async Task AuthResponse_ShouldDeclareEveryFieldAsRequired()
    {
        var schema = (await SchemasAsync()).GetProperty("AuthResponse");

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Contains("accessToken", required);
        Assert.Contains("refreshToken", required);
        Assert.Contains("playerId", required);

        Assert.All(schema.GetProperty("properties").EnumerateObject(),
            property => Assert.False(property.Value.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()));
    }

    /// <summary>Поле не може бути водночас обов'язковим і nullable — це суперечність для генератора.</summary>
    [Fact]
    public async Task Schemas_ShouldNotMarkNullablePropertiesAsRequired()
    {
        var contradictions = new List<string>();

        foreach (var schema in (await SchemasAsync()).EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("required", out var required)
                || !schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            foreach (var name in required.EnumerateArray().Select(e => e.GetString()!))
            {
                if (properties.TryGetProperty(name, out var property)
                    && property.TryGetProperty("nullable", out var nullable)
                    && nullable.GetBoolean())
                {
                    contradictions.Add($"{schema.Name}.{name}");
                }
            }
        }

        Assert.Empty(contradictions);
    }
}
