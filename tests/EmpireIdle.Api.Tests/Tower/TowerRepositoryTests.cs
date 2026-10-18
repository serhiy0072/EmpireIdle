using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Tower;

/// <summary>
/// Рейтинг вежі на справжньому Postgres: порядок «поверх ↓, час ↑», місце з урахуванням рівності
/// й фільтр сервера. Власний сервер тесту — щоб рядки інших тестів колекції не зсували місця.
/// </summary>
[Collection("postgres")]
public class TowerRepositoryTests : IAsyncLifetime
{
    private const int ServerId = 31;
    private static readonly DateTime Start = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public TowerRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static TowerProgress Progress(int floors, int minutes, int serverId = ServerId)
    {
        var progress = new TowerProgress(Guid.NewGuid(), Guid.NewGuid(), serverId);

        for (var floor = 1; floor <= floors; floor++)
            progress.Clear(floor, maxFloor: 1000, Start.AddMinutes(minutes));

        return progress;
    }

    [Fact]
    public async Task Ranking_ShouldOrderByFloorThenByWhoGotThereFirst_WithinTheServer()
    {
        var early = Progress(12, minutes: 1);
        var late = Progress(12, minutes: 5);
        var top = Progress(20, minutes: 9);
        var newcomer = new TowerProgress(Guid.NewGuid(), Guid.NewGuid(), ServerId);
        var stranger = Progress(99, minutes: 0, serverId: ServerId + 1);

        await using (var seed = CreateContext())
        {
            seed.TowerProgress.AddRange(early, late, top, newcomer, stranger);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var repository = new TowerRepository(context);

        var board = await repository.GetTopAsync(10);

        Assert.Equal([top.Id, early.Id, late.Id], board.Select(p => p.Id));
        Assert.Equal(2, await repository.GetRankAsync(early));
        Assert.Equal(3, await repository.GetRankAsync(late));
        Assert.Equal(1, await repository.GetRankAsync(top));
    }

    /// <summary>Другий рядок того самого гравця — гонка двох перших штурмів — відбиває унікальний індекс.</summary>
    [Fact]
    public async Task Progress_ShouldBeUniquePerPlayer()
    {
        var playerId = Guid.NewGuid();

        await using var context = CreateContext();
        context.TowerProgress.AddRange(new TowerProgress(Guid.NewGuid(), playerId, ServerId), new TowerProgress(Guid.NewGuid(), playerId, ServerId));

        await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
    }
}
