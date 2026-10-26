using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Inventory;

/// <summary>
/// Списали останній предмет і в тій самій транзакції видали його знову (скриня, що дає свій ключ):
/// видача мусить лягти в стек, а не зникнути разом із видаленим рядком.
/// </summary>
[Collection("postgres")]
public class InventoryRepositoryTests : IAsyncLifetime
{
    private const string ItemKey = "test_key";

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public InventoryRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task GetItemAsync_ShouldReviveAStackRemovedInTheSameUnitOfWork()
    {
        var playerId = Guid.NewGuid();

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            context.PlayerItems.Add(new PlayerItem(Guid.NewGuid(), playerId, ItemKey, 1));
            await context.SaveChangesAsync();
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var inventory = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();

            var stack = (await inventory.GetItemAsync(playerId, ItemKey))!;
            stack.Consume(1);
            inventory.RemoveItem(stack);

            var again = await inventory.GetItemAsync(playerId, ItemKey);
            Assert.NotNull(again);
            again.Add(2);

            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }

        await using var check = _factory.Services.CreateAsyncScope();
        var items = await check.ServiceProvider.GetRequiredService<IInventoryRepository>().GetItemsAsync(playerId);

        Assert.Equal(2, Assert.Single(items, i => i.ItemKey == ItemKey).Count);
    }
}
