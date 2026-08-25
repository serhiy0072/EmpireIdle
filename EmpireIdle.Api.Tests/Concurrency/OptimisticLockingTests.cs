using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Concurrency;

/// <summary>
/// Перевіряє, що оптимістичне блокування справді ловить гонки.
/// Кожен тест відтворює конкретний дюп із аудиту.
/// </summary>
[Collection("postgres")]
public class OptimisticLockingTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    public OptimisticLockingTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    /// <summary>
    /// Дюп армії: два паралельні марші з того самого гарнізону.
    /// Без токена обидва проходили б і 10 юнітів ставали 20.
    /// </summary>
    [Fact]
    public async Task Garrison_ShouldRejectConcurrentUnitWithdrawal()
    {
        var garrisonId = await SeedGarrisonAsync(infantry: 10);

        // Два контексти читають той самий стан — це і є гонка
        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var garrisonA = await LoadAsync(contextA, garrisonId);
        var garrisonB = await LoadAsync(contextB, garrisonId);

        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 }, DateTime.UtcNow);
        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 10 }, DateTime.UtcNow);

        await contextA.SaveChangesAsync();

        // Другий мусить впертись у змінений xmin
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        await using var verify = CreateContext();
        var final = await LoadAsync(verify, garrisonId);

        Assert.Equal(0, final.Units.Single(u => u.UnitType == "infantry").Count);
    }

    /// <summary>
    /// Токен на корені має спрацьовувати й тоді, коли змінились лише
    /// дочірні рядки — саме для цього існує UpdatedAt/Touch.
    /// </summary>
    [Fact]
    public async Task Garrison_ShouldDetectConflict_WhenOnlyChildRowsChanged()
    {
        var garrisonId = await SeedGarrisonAsync(infantry: 10);

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var garrisonA = await LoadAsync(contextA, garrisonId);
        var garrisonB = await LoadAsync(contextB, garrisonId);

        // SendUnits міняє тільки VillageUnit.Count — рядок Garrisons
        // оновиться лише завдяки Touch()
        garrisonA.SendUnits(new Dictionary<string, int> { ["infantry"] = 3 }, DateTime.UtcNow);
        garrisonB.SendUnits(new Dictionary<string, int> { ["infantry"] = 4 }, DateTime.UtcNow);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }

    /// <summary>
    /// Дзеркало гарнізонного тесту для села. GrantResources міняє лише
    /// VillageResources — рядок Villages оновлюється тільки через Touch().
    /// До коміта 83da803 (токен на корені) другий SaveChanges мовчки проходив.
    /// </summary>
    [Fact]
    public async Task Village_ShouldDetectConflict_WhenOnlyChildRowsChanged()
    {
        var villageId = await SeedVillageAsync();

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var villageA = await LoadVillageAsync(contextA, villageId);
        var villageB = await LoadVillageAsync(contextB, villageId);

        var reward = new List<ResourceCost> { new() { Resource = "gold", Amount = 50 } };

        villageA.GrantResources(reward, DateTime.UtcNow);
        villageB.GrantResources(reward, DateTime.UtcNow);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());
    }

    /// <summary>
    /// Інваріант «одна будівля кожного типу» тримається лише перевіркою в AddBuilding:
    /// унікальності (VillageId, Type) в БД немає. Токен на корені — єдине,
    /// що серіалізує два паралельні AddBuilding одного типу.
    /// </summary>
    [Fact]
    public async Task Village_ShouldRejectConcurrentDuplicateBuilding()
    {
        var villageId = await SeedVillageAsync();
        var buildings = _factory.Services.GetRequiredService<GameCatalog>().Buildings;

        await using var contextA = CreateContext();
        await using var contextB = CreateContext();

        var villageA = await LoadVillageAsync(contextA, villageId);
        var villageB = await LoadVillageAsync(contextB, villageId);

        // Обидва бачать село без ферми й обидва проходять перевірку унікальності
        villageA.AddBuilding("farm", buildings, DateTime.UtcNow);
        villageB.AddBuilding("farm", buildings, DateTime.UtcNow);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        await using var verify = CreateContext();
        var final = await LoadVillageAsync(verify, villageId);

        Assert.Single(final.Buildings, b => b.Type == "farm");
    }

    private AppDbContext CreateContext()
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Фонового HTTP-контексту немає — світ ставимо явно
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(1);

        return context;
    }

    private static Task<Garrison> LoadAsync(AppDbContext context, Guid id)
        => context.Garrisons.Include(g => g.Units).FirstAsync(g => g.Id == id);

    private async Task<Guid> SeedGarrisonAsync(int infantry)
    {
        await using var context = CreateContext();

        var garrison = new Garrison(Guid.NewGuid(), Guid.NewGuid());
        garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = infantry }, DateTime.UtcNow);

        context.Garrisons.Add(garrison);
        await context.SaveChangesAsync();

        return garrison.Id;
    }
    private static Task<Village> LoadVillageAsync(AppDbContext context, Guid id)
    => context.Villages
        .Include(v => v.Buildings)
        .Include(v => v.Resources)
        .AsSplitQuery()
        .FirstAsync(v => v.Id == id);

    private async Task<Guid> SeedVillageAsync()
    {
        await using var context = CreateContext();

        var village = new Village(
            Guid.NewGuid(), Guid.NewGuid(), "Concurrency Test",
            ["gold", "food", "wood", "iron", "population"], x: 5, y: 5);

        // Ферма коштує 100 gold + 50 wood — з запасом, щоб ChargeCost не був причиною падіння
        village.GrantStartingResources(
            new Dictionary<string, int> { ["gold"] = 1000, ["wood"] = 1000, ["food"] = 1000, ["iron"] = 1000 },
            DateTime.UtcNow);

        context.Villages.Add(village);
        await context.SaveChangesAsync();

        return village.Id;
    }
}
