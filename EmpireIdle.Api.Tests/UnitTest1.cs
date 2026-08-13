using Microsoft.AspNetCore.Mvc.Testing;

namespace EmpireIdle.Api.Tests;

/// <summary>Ловить помилки конфігурації та DI, які компілятор не бачить.</summary>
public class StartupTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public StartupTests(WebApplicationFactory<global::Program> factory) => _factory = factory;

    [Fact]
    public void Application_Starts()
    {
        // Кине, якщо ValidateOnStart не пройшов або DI не резолвиться
        var client = _factory.CreateClient();
        Assert.NotNull(client);
    }
}
