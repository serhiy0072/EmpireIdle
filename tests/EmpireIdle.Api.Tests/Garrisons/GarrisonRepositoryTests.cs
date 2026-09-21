using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Infrastructure.Persistence;
using EmpireIdle.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Garrisons;

/// <summary>
/// Регресія на реальний баг: репозиторію бракувало .Include(g => g.LevelUpOrders),
/// тож прокачка мовчки зникала — юніти йшли в чергу, а сама черга ніколи
/// не показувалась і сканер не бачив, що вона дозріла.
/// </summary>
[Collection("postgres")]
public class GarrisonRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public GarrisonRepositoryTests(PostgresFixture postgres) => _postgres = postgres;

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

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(1);

        return context;
    }

    /// <summary>Прокачка, поставлена в чергу й збережена, має пережити перезавантаження з нового контексту.</summary>
    [Fact]
    public async Task GetByVillageIdAsync_ShouldReturnLevelUpOrders_AfterASaveAndReload()
    {
        var villageId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            var garrison = new Garrison(Guid.NewGuid(), villageId, 1);
            garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, DateTime.UtcNow);
            garrison.LevelUpUnits("infantry", 1, 2, 4, maxBatchSize: 10, TimeSpan.FromMinutes(10), DateTime.UtcNow);

            seed.Garrisons.Add(garrison);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var repository = new GarrisonRepository(context);

        var reloaded = await repository.GetByVillageIdAsync(villageId, CancellationToken.None);

        var order = Assert.Single(reloaded!.LevelUpOrders);
        Assert.Equal("infantry", order.UnitType);
        Assert.Equal(4, order.Count);
    }

    /// <summary>Сканер має знаходити гарнізони з дозрілою прокачкою — інакше вона ніколи не завершиться сама.</summary>
    [Fact]
    public async Task GetIdsWithDueLevelUpsAsync_ShouldFindGarrisonsPastTheirDeadline()
    {
        var villageId = Guid.NewGuid();
        Guid garrisonId;

        await using (var seed = CreateContext())
        {
            var garrison = new Garrison(Guid.NewGuid(), villageId, 1);
            garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, DateTime.UtcNow);
            garrison.LevelUpUnits("infantry", 1, 2, 4, maxBatchSize: 10, TimeSpan.FromMinutes(-5), DateTime.UtcNow);

            seed.Garrisons.Add(garrison);
            await seed.SaveChangesAsync();
            garrisonId = garrison.Id;
        }

        await using var context = CreateContext();
        var repository = new GarrisonRepository(context);

        var ids = await repository.GetIdsWithDueLevelUpsAsync(DateTime.UtcNow, batchSize: 100, CancellationToken.None);

        Assert.Contains(garrisonId, ids);
    }
}
