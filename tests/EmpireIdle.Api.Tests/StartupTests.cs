using Microsoft.AspNetCore.Hosting;
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
        // Testing вимикає Hangfire: він кешує LoggerFactory у статиці,
        // а WebApplicationFactory будує хост двічі
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // Той самий конфіг, що в CI: тест перевіряє, що хост піднімається,
            // а не те, звідки застосунок бере секрети
            builder.UseSetting("JwtSettings:Secret", "test-only-signing-key-min-32-chars-long");
            builder.UseSetting("JwtSettings:Issuer", "EmpireIdle.Tests");
            builder.UseSetting("JwtSettings:Audience", "EmpireIdle.Tests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=placeholder");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        });

        // Кине, якщо ValidateOnStart не пройшов або DI не резолвиться
        var client = factory.CreateClient();
        Assert.NotNull(client);
    }
}
