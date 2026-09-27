using EmpireIdle.Api.Tests.Infrastructure;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EmpireIdle.Api.Tests.Servers;

/// <summary>
/// Медіана ратуш вирішує, чи дозрів світ до еволюції. Запит мусить перекластися в SQL
/// (рівень — властивість із конвертером) і рахувати лише свій світ: у будівлі немає ServerId.
/// Юніт-тест команди репозиторій мокає, тож без цього тесту обидві помилки жили б непоміченими.
/// </summary>
[Collection("postgres")]
public class MedianTownHallQueryTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private TestApiFactory _factory = null!;

    // Свій ключ ратуші на тест: спільна база, і чужі будівлі в медіану не потраплять
    private readonly string _townHall = $"th_{Guid.NewGuid():N}";

    public MedianTownHallQueryTests(PostgresFixture postgres) => _postgres = postgres;

    public async Task InitializeAsync()
    {
        _factory = new TestApiFactory(_postgres.ConnectionString);
        await _factory.MigrateAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private AsyncServiceScope CreateScope(int serverId, out IVillageRepository villages, out AppDbContext context)
    {
        var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IServerContext>().UseServer(serverId);
        villages = scope.ServiceProvider.GetRequiredService<IVillageRepository>();
        context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return scope;
    }

    private async Task SeedAsync(int serverId, params int[] townHallLevels)
    {
        await using var scope = CreateScope(serverId, out _, out var context);
        var config = new BuildingConfig { Key = _townHall, IsMainBuilding = true };
        var now = DateTime.UtcNow;

        foreach (var level in townHallLevels)
        {
            var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Median", [], 1, 1, serverId);
            village.AddBuilding(_townHall, new Dictionary<string, BuildingConfig> { [_townHall] = config }, now);

            var townHall = village.Buildings.Single();

            for (var i = 1; i < level; i++)
            {
                townHall.BeginUpgrade(config, TimeSpan.Zero, now, ProductionBoost.None, 1.0);
                townHall.CompleteConstruction(now);
            }

            context.Villages.Add(village);
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Median_ShouldBeTranslatedToSql_AndCountOnlyTheOwnWorld()
    {
        await SeedAsync(serverId: 1, 2, 5, 9);
        await SeedAsync(serverId: 2, 30, 30, 30, 30, 30);

        await using var scope = CreateScope(1, out var villages, out _);

        Assert.Equal(5, await villages.GetMedianMainBuildingLevelAsync(_townHall));
    }

    /// <summary>Парна кількість: верхня з двох середніх — Skip(count / 2).</summary>
    [Fact]
    public async Task Median_ShouldTakeTheUpperMiddle_ForAnEvenCount()
    {
        await SeedAsync(serverId: 1, 1, 3, 4, 8);

        await using var scope = CreateScope(1, out var villages, out _);

        Assert.Equal(4, await villages.GetMedianMainBuildingLevelAsync(_townHall));
    }

    [Fact]
    public async Task Median_ShouldBeZero_WithoutVillages()
    {
        await using var scope = CreateScope(1, out var villages, out _);

        Assert.Equal(0, await villages.GetMedianMainBuildingLevelAsync(_townHall));
    }
}
