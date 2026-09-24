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

    /// <summary>
    /// Клієнт рахує ціну прискорення сам, щосекунди. Параметри мусять збігатися
    /// з тими, за якими спише сервер, — інакше кнопка знову збреше.
    /// </summary>
    [Fact]
    public void Response_ShouldCarryTheSpeedUpPricing()
    {
        var config = new GameConfigBuilder().WithHeroes().Build();
        config.Monetization.InstantFinishThresholdMinutes = 7;
        config.Monetization.SpeedUpFactor = 2.5;
        config.Monetization.SpeedUpExponent = 0.6;

        var response = new GameCatalogProjection(new GameCatalog(config)).Response;

        Assert.Equal(new CatalogSpeedUp(7, 2.5, 0.6), response.SpeedUp);
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

    /// <summary>Без вартості й вимог клієнт або мовчить про ціну, або вигадує її сам.</summary>
    [Fact]
    public void Response_ShouldCarryUnitsWithCostAndRequirements()
    {
        var catalog = new GameConfigBuilder().WithBuildings().WithUnits(unit =>
        {
            unit.RequiresBuilding = TestKeys.Barracks;
            unit.RequiresBuildingLevel = 2;
            unit.BaseTrainMinutes = 3;
        }).BuildCatalog();

        var response = new GameCatalogProjection(catalog).Response;

        Assert.Equal(catalog.Config.Units.Count, response.Units.Count);
        Assert.All(response.Units, unit => Assert.False(string.IsNullOrWhiteSpace(unit.DisplayName)));
        Assert.All(response.Units, unit => Assert.Equal(TestKeys.Barracks, unit.RequiresBuilding));
        Assert.All(response.Units, unit => Assert.Equal(2, unit.RequiresBuildingLevel));
        Assert.All(response.Units, unit => Assert.Equal(3, unit.BaseTrainMinutes));
        Assert.All(response.Units, unit => Assert.NotEmpty(unit.Cost));
    }

    /// <summary>
    /// Кап рівня й крива вартості прокачки мусять доїхати до клієнта: без них
    /// екран не знає, скільки рівнів пропонувати й скільки це коштуватиме.
    /// </summary>
    [Fact]
    public void Response_ShouldCarryTheUnitLevelCapAndGrowthCurve()
    {
        var config = new GameConfigBuilder().WithBuildings().WithUnits(unit =>
        {
            unit.LevelUpCostGrowth = 1.35;
        }).Build();
        config.MaxUnitLevel = 7;

        var response = new GameCatalogProjection(new GameCatalog(config)).Response;

        Assert.Equal(7, response.MaxUnitLevel);
        Assert.All(response.Units, unit => Assert.Equal(1.35, unit.LevelUpCostGrowth));
    }

    /// <summary>
    /// Ціна лікування gems потрібна клієнту для прев'ю: юніти в госпіталі
    /// не несуть власної вартості, на відміну від відновлюваних.
    /// </summary>
    [Fact]
    public void Response_ShouldCarryTheHealGemsPrice()
    {
        var config = new GameConfigBuilder().WithBuildings().Build();
        config.Monetization.HealGemsPerUnit = 3;

        var response = new GameCatalogProjection(new GameCatalog(config)).Response;

        Assert.Equal(3, response.HealGemsPerUnit);
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

    /// <summary>Умова відкриття туману війни (GDD §3.1) — клієнт мусить знати, на якому рівні ратуші показати кнопку замість силуету.</summary>
    [Fact]
    public void Response_ShouldCarryTheMainBuildingLevelGate()
    {
        var config = new GameConfigBuilder().WithBuildings(TestKeys.Warehouse).Build();
        var warehouse = config.Buildings.Single(b => b.Key == TestKeys.Warehouse);
        warehouse.RequiresMainBuildingLevel = 4;

        var response = new GameCatalogProjection(new GameCatalog(config)).Response;
        var building = response.Buildings.Single(b => b.Key == TestKeys.Warehouse);

        Assert.Equal(4, building.RequiresMainBuildingLevel);
    }

    // ---------- Мови ----------

    /// <summary>Конфіг українською, англійська локаль перекладає ратушу, решта — з конфіга.</summary>
    private static GameCatalog WithEnglishLocale()
    {
        var config = new GameConfigBuilder().WithHeroes().WithEquipment().Build();

        config.Localization.Languages = ["uk", "en"];
        config.Locales["en"] = new LocaleConfig
        {
            Names = new Dictionary<string, string> { [$"building.{TestKeys.Townhall}"] = "Town Hall" }
        };

        return new GameCatalog(config);
    }

    [Fact]
    public void ResponseFor_ShouldTranslateNamesFromTheLocale()
    {
        var catalog = WithEnglishLocale();
        var projection = new GameCatalogProjection(catalog);

        var english = projection.ResponseFor("en");

        Assert.Equal("en", english.Language);
        Assert.Equal("Town Hall", english.Buildings.Single(b => b.Key == TestKeys.Townhall).DisplayName);

        // Чого в локалі немає — з конфіга
        var hall = catalog.Config.Buildings.Single(b => b.Key == TestKeys.Hall);
        Assert.Equal(hall.DisplayName, english.Buildings.Single(b => b.Key == TestKeys.Hall).DisplayName);
    }

    /// <summary>Непідтримувана мова — не помилка, а каталог мовою за замовчуванням.</summary>
    [Fact]
    public void ResponseFor_ShouldFallBackToTheDefaultLanguage()
    {
        var projection = new GameCatalogProjection(WithEnglishLocale());

        var german = projection.ResponseFor("de");

        Assert.Equal("uk", german.Language);
        Assert.Equal(projection.Response.Version, german.Version);
    }

    /// <summary>Різні мови — різні версії: інакше ETag віддав би 304 на чужу мову.</summary>
    [Fact]
    public void ResponseFor_ShouldVersionEachLanguageSeparately()
    {
        var projection = new GameCatalogProjection(WithEnglishLocale());

        Assert.NotEqual(projection.ResponseFor("uk").Version, projection.ResponseFor("en").Version);
    }

    // ---------- Набори артефактів ----------

    /// <summary>
    /// Родина несе все, що потрібне екрану наборів: назву, рівень у числах,
    /// характер і данж-джерело з порогом ратуші.
    /// </summary>
    [Fact]
    public void Response_ShouldDescribeEachArtifactSetFamily()
    {
        var catalog = new GameConfigBuilder()
            // Спорядження першим: WithDungeons лише доповнює його своїм набором
            .WithEquipment(equipment => equipment.ArtifactTierMultipliers = [1.0, 1.35])
            .WithDungeons()
            .BuildCatalog();

        var set = new GameCatalogProjection(catalog).Response.ArtifactSets.Single();

        Assert.Equal(TestKeys.DungeonSetKey, set.Key);
        Assert.Equal("Набір Ями", set.DisplayName);
        Assert.Equal(1, set.Tier);
        Assert.Equal(1.0, set.StatMultiplier);
        Assert.Equal(["Attack", "Health"], set.FocusStats);
        Assert.Equal(TestKeys.Dungeon, set.Dungeon?.Key);
        Assert.Equal(1, set.Dungeon?.RequiresMainBuildingLevel);
    }

    /// <summary>Кожна рідкість — зі своїм бонусом і чотирма частинами; ключ збігається з SetKey предметів.</summary>
    [Fact]
    public void Response_ShouldListEveryRarityWithItsBonusAndPieces()
    {
        var catalog = new GameConfigBuilder().WithDungeons().BuildCatalog();

        var set = new GameCatalogProjection(catalog).Response.ArtifactSets.Single();

        Assert.Equal(["Common", "Rare", "Unique"], set.Rarities.Select(r => r.Rarity));

        Assert.All(set.Rarities, rarity =>
        {
            Assert.Equal($"{TestKeys.DungeonSetKey}_{rarity.Rarity.ToLowerInvariant()}", rarity.SetKey);
            Assert.Equal(4, rarity.RequiredPieces);
            Assert.Equal(10, rarity.Bonus["Attack"]);
            Assert.Equal(4, rarity.PieceKeys.Count);
            Assert.All(rarity.PieceKeys, key => Assert.Equal(rarity.SetKey, catalog.FindItem(key)?.SetKey));
        });
    }
}
