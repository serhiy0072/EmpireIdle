using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// «Знайди або створи» в одній одиниці роботи. Пакетна видача (10-ролл, «Забрати все»)
/// кладе кілька однакових предметів чи героїв в одну транзакцію; якщо пошук бачить лише
/// базу, друга видача робить другий INSERT на унікальний індекс і відкочує весь ролл.
/// На InMemory цього не видно — там немає унікальних індексів, тож тест на Postgres.
/// </summary>
[Collection("postgres")]
public class UnitOfWorkLookupTests : IAsyncLifetime
{
    private const int ServerId = 1;

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public UnitOfWorkLookupTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(out AppDbContext context)
    {
        var scope = _factory.Services.CreateAsyncScope();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return scope;
    }

    private async Task<(Guid PlayerId, Guid GarrisonId)> SeedPlayerAsync()
    {
        await using var scope = CreateScope(out var context);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var player = new Player(Guid.NewGuid(), $"p{suffix}", $"{suffix}@test.local", $"user-{suffix}", DateTime.UtcNow, ServerId);
        var village = new Village(Guid.NewGuid(), player.Id, $"Village {suffix}", [], 1, 1, ServerId);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, ServerId);

        context.Players.Add(player);
        context.Villages.Add(village);
        context.Garrisons.Add(garrison);
        await context.SaveChangesAsync();

        return (player.Id, garrison.Id);
    }

    /// <summary>Два однакові предмети в одній серії — один рядок із лічильником 2, без 23505.</summary>
    [Fact]
    public async Task GetItemAsync_ShouldSeeAnItemAddedInTheSameUnitOfWork()
    {
        var (playerId, _) = await SeedPlayerAsync();

        await using (var scope = CreateScope(out var context))
        {
            var inventory = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

            foreach (var _ in Enumerable.Range(0, 2))
            {
                var item = await inventory.GetItemAsync(playerId, "teleport");

                if (item is null)
                    await inventory.AddItemAsync(new PlayerItem(Guid.NewGuid(), playerId, "teleport", 1));
                else
                    item.Add(1);
            }

            await context.SaveChangesAsync();
        }

        await using var check = CreateScope(out var reader);
        var stored = await reader.PlayerItems.Where(i => i.PlayerId == playerId).ToListAsync();

        Assert.Equal(2, Assert.Single(stored).Count);
    }

    /// <summary>Герой, виданий раніше в тій самій серії, знаходиться за ключем — дубль іде в сузір'я.</summary>
    [Fact]
    public async Task GetByKeyAsync_ShouldSeeAHeroAddedInTheSameUnitOfWork()
    {
        var (playerId, garrisonId) = await SeedPlayerAsync();

        await using var scope = CreateScope(out _);
        var heroes = scope.ServiceProvider.GetRequiredService<IHeroRepository>();

        var hero = new Hero(Guid.NewGuid(), playerId, ServerId, "knight", garrisonId, asLeader: false, DateTime.UtcNow);
        await heroes.AddAsync(hero);

        Assert.Same(hero, await heroes.GetByKeyAsync(playerId, "knight"));
    }

    /// <summary>Лідер, призначений у цій же транзакції, займає слот — другий не стане лідером.</summary>
    [Fact]
    public async Task GetLeaderAsync_ShouldSeeALeaderAddedInTheSameUnitOfWork()
    {
        var (playerId, garrisonId) = await SeedPlayerAsync();

        await using var scope = CreateScope(out _);
        var heroes = scope.ServiceProvider.GetRequiredService<IHeroRepository>();

        var leader = new Hero(Guid.NewGuid(), playerId, ServerId, "knight", garrisonId, asLeader: true, DateTime.UtcNow);
        await heroes.AddAsync(leader);

        Assert.Same(leader, await heroes.GetLeaderAsync(garrisonId, playerId));
    }

    /// <summary>Лідерство зняли в пам'яті, ще не зберігши, — база більше не відповідає за нього.</summary>
    [Fact]
    public async Task GetLeaderAsync_ShouldIgnoreALeaderDismissedInTheSameUnitOfWork()
    {
        var (playerId, garrisonId) = await SeedPlayerAsync();
        var leaderId = Guid.NewGuid();

        await using (var seed = CreateScope(out var context))
        {
            context.Heroes.Add(new Hero(leaderId, playerId, ServerId, "knight", garrisonId, asLeader: true, DateTime.UtcNow));
            await context.SaveChangesAsync();
        }

        await using var scope = CreateScope(out _);
        var heroes = scope.ServiceProvider.GetRequiredService<IHeroRepository>();

        var stored = await heroes.GetByIdAsync(leaderId);
        stored!.DismissLeader(DateTime.UtcNow);

        Assert.Null(await heroes.GetLeaderAsync(garrisonId, playerId));
    }
}
