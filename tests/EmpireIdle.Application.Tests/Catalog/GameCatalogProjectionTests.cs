using EmpireIdle.Application.Catalog;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Application.Tests.Catalog;

/// <summary>
/// Каталог — єдине джерело назв для клієнта. Якщо він загубить героя
/// чи предмет, екран покаже голий ключ і ніхто цього не помітить до релізу.
/// </summary>
public class GameCatalogProjectionTests
{
    private readonly GameCatalogProjection _projection =
        new(new GameConfigBuilder().WithHeroes().WithEquipment().BuildCatalog());

    [Fact]
    public void Response_ShouldCarryEveryHeroWithItsName()
    {
        var catalog = new GameConfigBuilder().WithHeroes().WithEquipment().BuildCatalog();

        var response = new GameCatalogProjection(catalog).Response;

        Assert.Equal(catalog.Config.Heroes.Count, response.Heroes.Count);
        Assert.All(response.Heroes, hero => Assert.False(string.IsNullOrWhiteSpace(hero.DisplayName)));
    }

    /// <summary>Ранг і слот ідуть рядками: число в типах клієнта вимагало б власної мапи.</summary>
    [Fact]
    public void Response_ShouldSpellOutRankAndSlot()
    {
        var response = _projection.Response;

        Assert.All(response.Heroes, hero => Assert.Contains(hero.Rank, new[] { "Common", "Rare", "Unique" }));
        Assert.All(response.Items.Where(item => item.Slot is not null),
            item => Assert.Contains(item.Slot, new[] { "Weapon", "Artifact" }));
    }

    /// <summary>Швидкість підставляється з налаштувань, якщо в героя її немає.</summary>
    [Fact]
    public void Response_ShouldResolveSpeedFromSettings()
        => Assert.All(_projection.Response.Heroes, hero => Assert.True(hero.Speed > 0));

    [Fact]
    public void Response_ShouldCarryResourcesAndBuildings()
    {
        var response = _projection.Response;

        Assert.NotEmpty(response.Resources);
        Assert.All(response.Resources, resource => Assert.False(string.IsNullOrWhiteSpace(resource.DisplayName)));
        Assert.All(response.Buildings, building => Assert.False(string.IsNullOrWhiteSpace(building.DisplayName)));
    }

    /// <summary>Та сама проєкція — та сама версія: інакше ETag мінявся б щозапиту.</summary>
    [Fact]
    public void Version_ShouldBeStableAcrossReads()
        => Assert.Equal(_projection.Response.Version, _projection.Response.Version);

    [Fact]
    public void Version_ShouldChangeWithTheConfig()
    {
        var other = new GameConfigBuilder().WithHeroes().Build();
        other.Heroes[0].DisplayName = "Renamed";

        var changed = new GameCatalogProjection(new EmpireIdle.Domain.Services.GameCatalog(other)).Response;

        Assert.NotEqual(_projection.Response.Version, changed.Version);
    }

    /// <summary>Позиція будує розкладку села на клієнті: без неї будівлю нема куди поставити.</summary>
    [Fact]
    public void Response_ShouldCarryBuildingPositions()
    {
        var config = new GameConfigBuilder().WithBuildings(TestKeys.Warehouse).Build();
        var townhall = config.Buildings[0];
        townhall.Position = new BuildingPosition { X = 50, Y = 30 };

        var response = new GameCatalogProjection(new GameCatalog(config)).Response;
        var building = response.Buildings.Single(b => b.Key == townhall.Key);

        Assert.Equal(new CatalogPosition(50, 30), building.Position);
    }
}
