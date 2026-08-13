using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace EmpireIdle.Api.Tests;

/// <summary>Ловить помилки конфігурації та DI, які компілятор не бачить.</summary>
public class StartupTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StartupTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public void Application_Starts()
    {
        // Кине, якщо ValidateOnStart не пройшов або DI не резолвиться
        var client = _factory.CreateClient();
        Assert.NotNull(client);
    }
}
