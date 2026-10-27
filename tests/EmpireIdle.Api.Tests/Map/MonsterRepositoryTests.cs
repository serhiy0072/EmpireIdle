using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Map;

/// <summary>
/// Пошук найближчого монстра на справжньому Postgres: відстань рахує база, тип і сервер
/// фільтруються. Власний сервер тесту — щоб монстри інших тестів колекції не заважали.
/// </summary>
[Collection("postgres")]
public class MonsterRepositoryTests : IAsyncLifetime
{
    private const int ServerId = 32;
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public MonsterRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AppDbContext CreateContext()
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(ServerId);

        return context;
    }

    [Fact]
    public async Task GetNearestAsync_ShouldPickTheClosestOfTheType_WeakerFirstOnATie()
    {
        var far = new Monster(Guid.NewGuid(), ServerId, "wolf", 1, 20, 20, Now);
        var tieStrong = new Monster(Guid.NewGuid(), ServerId, "wolf", 9, 3, 0, Now);
        var tieWeak = new Monster(Guid.NewGuid(), ServerId, "wolf", 2, 0, -3, Now);
        var otherType = new Monster(Guid.NewGuid(), ServerId, "bear", 1, 1, 0, Now);
        var otherServer = new Monster(Guid.NewGuid(), ServerId + 1, "wolf", 1, 0, 1, Now);

        await using (var seed = CreateContext())
        {
            seed.Monsters.AddRange(far, tieStrong, tieWeak, otherType, otherServer);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var repository = new MonsterRepository(context);

        var nearest = await repository.GetNearestAsync(ServerId, "wolf", 0, 0);
        var none = await repository.GetNearestAsync(ServerId, "griffin", 0, 0);

        Assert.Equal(tieWeak.Id, nearest?.Id);
        Assert.Null(none);
    }
}
