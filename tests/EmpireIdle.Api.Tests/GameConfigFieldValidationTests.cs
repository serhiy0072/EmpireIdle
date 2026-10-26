using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure.Persistence.Outbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EmpireIdle.Api.Tests;

/// <summary>
/// Межі окремих полів конфіга гри — правила Validate з Program.cs. Описка в JSON має зупинити
/// старт, а не тихо зламати механіку: юніти з нічого в госпіталі, допомога понад увесь таймер,
/// щоденне зняття всіх лідерів, безплатний checkout.
/// </summary>
public class GameConfigFieldValidationTests : IClassFixture<GameConfigFieldValidationTests.Host>
{
    private readonly Host _host;

    public GameConfigFieldValidationTests(Host host) => _host = host;

    [Fact]
    public void ShippedConfig_ShouldPassEveryFieldRule()
        => Assert.Empty(_host.Failures(_ => { }));

    public static TheoryData<string, Action<GameConfig>> BrokenFields => new()
    {
        { "WoundedShareMax", c => c.Combat.WoundedShareMax = 1.5 },
        { "WoundedShareMax", c => c.Combat.WoundedShareMin = c.Combat.WoundedShareMax + 0.1 },
        { "NoLossShareThreshold", c => c.Combat.NoLossShareThreshold = 1 },
        { "RecoveryWindowHours", c => c.Combat.RecoveryWindowHours = 0 },
        { "loss bands", c => c.Combat.DefenderLossLosses.Max = 1.2 },
        { "CityFall", c => c.Combat.CityFall.DefeatsToEvict = 0 },
        { "CityFall", c => c.Combat.CityFall.DamagedProductionMultiplier = 2 },
        { "HelpSharePerPlayer", c => c.Clan.HelpSharePerPlayer = 0 },
        { "HelpSharePerPlayer", c => c.Clan.MaxHelpShare = 1.5 },
        { "LeaderInactivityDays", c => c.Clan.LeaderInactivityDays = 0 },
        { "RegenHours", c => c.Dungeons.TeamSize = 0 },
        { "DefenseSoftening", c => c.Dungeons.DefenseSoftening = 0 },
        { "DefenseSoftening", c => c.Dungeons.CritMultiplier = 0.5 },
        { "BaseTrainSeconds", c => c.Units[0].TrainTimeGrowth = 0.9 },
        { "BuildTimeGrowth", c => c.Buildings[0].BuildTimeGrowth = 0.5 },
        { "PriceCents", c => c.Shop.GemPacks[0].PriceCents = 0 },
        { "HealGemsPerUnit", c => c.Monetization.HealGemsPerUnit = -1 },
        { "activity points", c => c.Rating.PointsPerQuest = -5 },
        { "MoveCost", c => c.Map.Terrains[0].MoveCost = 0 }
    };

    [Theory]
    [MemberData(nameof(BrokenFields))]
    public void BrokenField_ShouldBeRejected(string expectedInMessage, Action<GameConfig> breakIt)
        => Assert.Contains(_host.Failures(breakIt), f => f.Contains(expectedInMessage));

    /// <summary>Outbox — не баланс, але BatchSize 0 крутить процесор без паузи: старт мусить упасти.</summary>
    [Fact]
    public void Outbox_ShouldRejectAZeroBatch()
    {
        var failures = _host.Services.GetServices<IValidateOptions<OutboxSettings>>()
            .Select(v => v.Validate(Options.DefaultName, new OutboxSettings { BatchSize = 0 }))
            .Where(r => r.Failed)
            .ToList();

        Assert.NotEmpty(failures);
    }

    /// <summary>Справжній хост із зареєстрованими правилами; кожен тест бере свіжу копію конфіга.</summary>
    public sealed class Host : IDisposable
    {
        private readonly WebApplicationFactory<global::Program> _factory = new WebApplicationFactory<global::Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting("JwtSettings:Secret", "test-only-signing-key-min-32-chars-long");
                builder.UseSetting("JwtSettings:Issuer", "EmpireIdle.Tests");
                builder.UseSetting("JwtSettings:Audience", "EmpireIdle.Tests");
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=placeholder");
                builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
                builder.UseSetting("StripeSettings:SecretKey", "sk_test_unused");
                builder.UseSetting("StripeSettings:WebhookSecret", "whsec_unused");
            });

        public IServiceProvider Services => _factory.Services;

        public IReadOnlyList<string> Failures(Action<GameConfig> mutate)
        {
            var config = _factory.Services.GetRequiredService<IConfiguration>().GetSection("GameConfig").Get<GameConfig>()!;
            mutate(config);

            return _factory.Services.GetServices<IValidateOptions<GameConfig>>()
                .Select(v => v.Validate(Options.DefaultName, config))
                .Where(r => r.Failed)
                .SelectMany(r => r.Failures ?? [])
                .ToList();
        }

        public void Dispose() => _factory.Dispose();
    }
}
