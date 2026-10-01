using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Api.Tests;

/// <summary>Ловить помилки конфігурації та DI, які компілятор не бачить.</summary>
public class StartupTests : IClassFixture<WebApplicationFactory<global::Program>>
{
    private readonly WebApplicationFactory<global::Program> _factory;

    public StartupTests(WebApplicationFactory<global::Program> factory) => _factory = factory;

    [Fact]
    public void Application_Starts()
    {
        using var factory = Configure(stripeSecretKey: "sk_test_unused");

        // Кине, якщо ValidateOnStart не пройшов або DI не резолвиться
        var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void Application_WithoutStripeKey_FailsOnStart()
    {
        using var factory = Configure(stripeSecretKey: "");

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("StripeSettings.SecretKey", exception.Message);
    }

    private WebApplicationFactory<global::Program> Configure(string stripeSecretKey) =>
        _factory.WithWebHostBuilder(builder =>
        {
            // Testing вимикає Hangfire: він кешує LoggerFactory у статиці,
            // а WebApplicationFactory будує хост двічі
            builder.UseEnvironment("Testing");

            // Той самий конфіг, що в CI: тест перевіряє, що хост піднімається,
            // а не те, звідки застосунок бере секрети
            builder.UseSetting("JwtSettings:Secret", "test-only-signing-key-min-32-chars-long");
            builder.UseSetting("JwtSettings:Issuer", "EmpireIdle.Tests");
            builder.UseSetting("JwtSettings:Audience", "EmpireIdle.Tests");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=placeholder");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
            builder.UseSetting("StripeSettings:SecretKey", stripeSecretKey);
            builder.UseSetting("StripeSettings:WebhookSecret", "whsec_unused");
        });
}
