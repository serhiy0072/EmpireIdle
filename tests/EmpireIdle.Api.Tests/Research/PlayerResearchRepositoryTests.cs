using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Research;

/// <summary>
/// Особисті технології на справжньому Postgres: рівні в jsonb переживають збереження, сканер бачить
/// лише дозрілі дослідження свого світу, а другий рядок гравця відбиває унікальний індекс.
/// </summary>
[Collection("postgres")]
public class PlayerResearchRepositoryTests : IAsyncLifetime
{
    private const int ServerId = 37;
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public PlayerResearchRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AppDbContext CreateContext(int serverId = ServerId)
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);

        return context;
    }

    [Fact]
    public async Task Research_ShouldKeepLevelsAndTheActiveTimer_AcrossSaves()
    {
        var research = new PlayerResearch(Guid.NewGuid(), Guid.NewGuid(), ServerId, Now);
        research.Start(new TechnologyKey(1, "attack_infantry"), TimeSpan.Zero, Now);
        research.CompleteDue(Now);
        research.Start(new TechnologyKey(1, "attack_archer"), TimeSpan.FromMinutes(20), Now);

        await using (var seed = CreateContext())
        {
            seed.PlayerResearch.Add(research);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var saved = await new PlayerResearchRepository(context).GetByPlayerAsync(research.PlayerId);

        Assert.NotNull(saved);
        Assert.Equal(1, saved.LevelOf(new TechnologyKey(1, "attack_infantry")));
        Assert.Equal(("1:attack_archer", 1, Now.AddMinutes(20)), (saved.ActiveKey, saved.ActiveLevel, saved.CompletesAt));
    }

    /// <summary>Сканер бере лише дозріле й лише свого світу: чуже дозріле на іншому сервері не чіпає.</summary>
    [Fact]
    public async Task DueIds_ShouldListOnlyRipeResearchOfTheWorld()
    {
        var ripe = new PlayerResearch(Guid.NewGuid(), Guid.NewGuid(), ServerId, Now);
        ripe.Start(new TechnologyKey(1, "prod_gold"), TimeSpan.FromMinutes(5), Now);
        var running = new PlayerResearch(Guid.NewGuid(), Guid.NewGuid(), ServerId, Now);
        running.Start(new TechnologyKey(1, "prod_gold"), TimeSpan.FromHours(5), Now);
        var stranger = new PlayerResearch(Guid.NewGuid(), Guid.NewGuid(), ServerId + 1, Now);
        stranger.Start(new TechnologyKey(1, "prod_gold"), TimeSpan.FromMinutes(5), Now);

        await using (var seed = CreateContext())
        {
            seed.PlayerResearch.AddRange(ripe, running);
            await seed.SaveChangesAsync();
        }

        await using (var other = CreateContext(ServerId + 1))
        {
            other.PlayerResearch.Add(stranger);
            await other.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var due = await new PlayerResearchRepository(context).GetIdsWithDueResearchAsync(Now.AddMinutes(10), 100);

        Assert.Contains(ripe.Id, due);
        Assert.DoesNotContain(running.Id, due);
        Assert.DoesNotContain(stranger.Id, due);
    }

    /// <summary>Два перші дослідження з різних вкладок — другий рядок того самого гравця відбиває унікальний індекс.</summary>
    [Fact]
    public async Task SecondRowOfAPlayer_ShouldBeRejectedByTheUniqueIndex()
    {
        var playerId = Guid.NewGuid();

        await using (var first = CreateContext())
        {
            first.PlayerResearch.Add(new PlayerResearch(Guid.NewGuid(), playerId, ServerId, Now));
            await first.SaveChangesAsync();
        }

        await using var second = CreateContext();
        second.PlayerResearch.Add(new PlayerResearch(Guid.NewGuid(), playerId, ServerId, Now));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }
}
