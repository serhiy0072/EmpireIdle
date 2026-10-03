using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using EmpireIdle.API.Jobs;
using EmpireIdle.API.Services;
using EmpireIdle.Domain.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

    /// <summary>
    /// Міжсекційну узгодженість перевіряє каталог у hosted-сервісі. Перевіряємо реєстрацію
    /// в справжньому Program: конфіг гри тут не підмінити — JSON-файли додаються після
    /// UseSetting і перекривають його.
    /// </summary>
    [Fact]
    public void Application_RegistersTheCatalogStartupCheck()
    {
        using var factory = Configure(stripeSecretKey: "sk_test_unused");
        factory.CreateClient();

        Assert.Contains(factory.Services.GetServices<IHostedService>(), s => s is GameCatalogStartupCheck);
    }

    /// <summary>Межі окремого поля — правила Validate з Program.cs, зареєстровані для GameConfig.</summary>
    [Fact]
    public void Application_RejectsAnInvertedCombatRandomRange()
    {
        using var factory = Configure(stripeSecretKey: "sk_test_unused");
        factory.CreateClient();

        var config = factory.Services.GetRequiredService<IOptions<GameConfig>>().Value;
        config.Combat.RandomMin = config.Combat.RandomMax + 1;

        var failures = factory.Services.GetServices<IValidateOptions<GameConfig>>()
            .Select(v => v.Validate(Options.DefaultName, config))
            .Where(r => r.Failed)
            .SelectMany(r => r.Failures ?? [])
            .ToList();

        Assert.Contains(failures, f => f.Contains("RandomMin"));
    }

    /// <summary>
    /// Hangfire сам створить джоб, якого немає в контейнері, тож пропуск не падає, а тихо
    /// обходить scoped-життя. Кожен клас *Job має бути зареєстрований явно.
    /// </summary>
    [Fact]
    public void Application_RegistersEveryJob()
    {
        using var factory = Configure(stripeSecretKey: "sk_test_unused");
        factory.CreateClient();

        var registry = factory.Services.GetRequiredService<IServiceProviderIsService>();
        var missing = typeof(ServerJobRunner).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ServerJobRunner).Namespace && t.Name.EndsWith("Job"))
            .Where(t => !registry.IsService(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missing);
    }

    private WebApplicationFactory<global::Program> Configure(string stripeSecretKey, params (string Key, string Value)[] overrides) =>
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

            foreach (var (key, value) in overrides)
                builder.UseSetting(key, value);
        });
}
