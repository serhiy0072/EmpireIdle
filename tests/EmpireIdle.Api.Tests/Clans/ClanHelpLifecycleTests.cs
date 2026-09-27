using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Clans;

/// <summary>
/// Запит допомоги живе рівно стільки, скільки таймер. Building.Id між апгрейдами той самий,
/// а індекс на TargetId унікальний — старий запит, що лишився, блокував би будівлю назавжди.
/// </summary>
[Collection("postgres")]
public class ClanHelpLifecycleTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public ClanHelpLifecycleTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(out IClanHelpRepository help, out AppDbContext context)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        help = scope.ServiceProvider.GetRequiredService<IClanHelpRepository>();
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return scope;
    }

    private async Task SeedAsync(Guid targetId, DateTime expiresAt, DateTime now)
    {
        await using var scope = CreateScope(out _, out var context);

        context.ClanHelpRequests.Add(new ClanHelpRequest(Guid.NewGuid(), ServerId, Guid.NewGuid(), Guid.NewGuid(),
            ClanHelpTarget.Construction, targetId, TimeSpan.FromHours(1), expiresAt, now.AddHours(-2)));
        await context.SaveChangesAsync();
    }

    /// <summary>Прострочений запит не рахується й прибирається — новий на ту саму будівлю вставляється.</summary>
    [Fact]
    public async Task ExpiredRequest_ShouldNotBlockANewOneForTheSameBuilding()
    {
        var now = DateTime.UtcNow;
        var buildingId = Guid.NewGuid();
        await SeedAsync(buildingId, expiresAt: now.AddMinutes(-1), now);

        await using var scope = CreateScope(out var help, out var context);

        Assert.False(await help.ExistsActiveForTargetAsync(buildingId, now));
        Assert.Equal(1, await help.RemoveForTargetAsync(buildingId, expiredBefore: now));

        await help.AddAsync(new ClanHelpRequest(Guid.NewGuid(), ServerId, Guid.NewGuid(), Guid.NewGuid(),
            ClanHelpTarget.Construction, buildingId, TimeSpan.FromHours(1), now.AddHours(1), now));
        await context.SaveChangesAsync();

        Assert.True(await help.ExistsActiveForTargetAsync(buildingId, now));
    }

    /// <summary>Чинний запит прибирання прострочених не чіпає.</summary>
    [Fact]
    public async Task RemovingExpired_ShouldKeepAnActiveRequest()
    {
        var now = DateTime.UtcNow;
        var buildingId = Guid.NewGuid();
        await SeedAsync(buildingId, expiresAt: now.AddHours(1), now);

        await using var scope = CreateScope(out var help, out _);

        Assert.Equal(0, await help.RemoveForTargetAsync(buildingId, expiredBefore: now));
        Assert.True(await help.ExistsActiveForTargetAsync(buildingId, now));
    }

    /// <summary>Апгрейд завершився (зокрема прискорений) — запит іде, навіть якщо ще не прострочений.</summary>
    [Fact]
    public async Task CompletedUpgrade_ShouldRemoveTheRequestAtOnce()
    {
        var now = DateTime.UtcNow;
        var buildingId = Guid.NewGuid();
        await SeedAsync(buildingId, expiresAt: now.AddHours(1), now);

        await using var scope = CreateScope(out var help, out var context);

        Assert.Equal(1, await help.RemoveForTargetAsync(buildingId));
        Assert.False(await context.ClanHelpRequests.AnyAsync(r => r.TargetId == buildingId));
    }
}
