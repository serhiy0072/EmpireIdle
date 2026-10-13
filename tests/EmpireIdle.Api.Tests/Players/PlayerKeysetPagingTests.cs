using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Players;

/// <summary>
/// Розсилка нагород серверного квесту йде по гравцях світу keyset-пачками (GDD §8.4).
/// Порівняння Guid мусить перекластися в SQL, пачки — не перетинатися й не губити гравців,
/// а чужий світ — не потрапляти в розсилку. Юніт-тест обробника репозиторій мокає,
/// тож без цього тесту всі три помилки жили б непоміченими.
/// </summary>
[Collection("postgres")]
public class PlayerKeysetPagingTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public PlayerKeysetPagingTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(int serverId, out IPlayerRepository players, out AppDbContext context)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);
        players = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return scope;
    }

    private async Task<List<Guid>> SeedAsync(int serverId, int count)
    {
        await using var scope = CreateScope(serverId, out _, out var context);
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var suffix = Guid.NewGuid().ToString("N")[..12];
            var player = new Player(Guid.NewGuid(), $"p{suffix}", $"{suffix}@test.local", $"user-{suffix}", DateTime.UtcNow, serverId);

            context.Players.Add(player);
            ids.Add(player.Id);
        }

        await context.SaveChangesAsync();

        return ids;
    }

    [Fact]
    public async Task GetIdsAfter_ShouldWalkTheOwnWorldInDisjointBatches()
    {
        var own = await SeedAsync(serverId: 1, count: 7);
        var foreign = await SeedAsync(serverId: 2, count: 3);

        await using var scope = CreateScope(1, out var players, out _);

        // База спільна з іншими тестами: проходимо світ до кінця й перевіряємо властивості, а не точний склад
        var walked = new List<Guid>();
        Guid? cursor = null;

        while (true)
        {
            var batch = await players.GetIdsAfterAsync(cursor, take: 3);

            if (batch.Count == 0)
                break;

            Assert.True(batch.Count <= 3);
            walked.AddRange(batch);
            cursor = batch[^1];
        }

        Assert.Equal(walked.Count, walked.Distinct().Count());
        Assert.All(own, id => Assert.Contains(id, walked));
        Assert.All(foreign, id => Assert.DoesNotContain(id, walked));
    }
}
