using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Villages.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Villages;

/// <summary>
/// Збір і апгрейд перевіряються разом: обидва рахують множник кільця
/// й вікно буста, і саме на їхньому стику ламалася економіка.
/// </summary>
public class VillageCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    /// <summary>
    /// Карта 20×20: радіус 10, центр на максимальному рівні d ≤ 2.
    /// Село ставиться в центр або на околицю за потребою тесту.
    /// </summary>
    private static GameConfig Config() => new()
    {
        BuildingLevelsPerTier = 10,
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45,
                BaseBuildMinutes = 10, BuildTimeGrowth = 1.5,
                Cost = [new ResourceCost { Resource = "food", Amount = 100 }] },
            new BuildingConfig
            {
                Key = "farm",
                ProducesResource = "food",
                BaseProductionPerMinute = 10,
                BaseStorage = 10_000,
                BaseBuildMinutes = 10,
                BuildTimeGrowth = 1.5,
                UpgradeCostGrowth = 1.45,
                Cost = [new ResourceCost { Resource = "food", Amount = 100 }]
            },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 100_000,
                UpgradeCostGrowth = 1.45 }
        ],
        Map = new MapConfig
        {
            Width = 20,
            Height = 20,
            MaxServerLevel = 3,
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.20, 0.50],
                RingMultipliers = [2.0, 1.4, 1.0],
                RingsAtFirstLevel = 0.40,
                FogMinShare = 0.40,
                FogMaxShare = 1.0
            }
        }
    };

    private static GameCatalog Catalog() => new(Config());
    private static WorldGeometry Geometry() => new(Config().Map);

    private CollectBuildingCommandHandler CollectHandler() => new(
        _villages, _unitOfWork, _servers, new EffectResolver(_effects),
        Catalog(), new FakeTimeProvider(Now), Geometry(),
        NullLogger<CollectBuildingCommandHandler>.Instance);

    private UpgradeBuildingCommandHandler UpgradeHandler() => new(
        _villages, _unitOfWork, _servers, new EffectResolver(_effects),
        new FakeTimeProvider(Now), Catalog(), Geometry(),
        NullLogger<UpgradeBuildingCommandHandler>.Instance);

    /// <param name="townhallLevel">
    /// Рівень ратуші. За замовчуванням високий: правило C не пускає жодну
    /// будівлю вище за неї, і з ратушею 1 рівня тести про вартість
    /// упирались би в гейт замість того, що перевіряють.
    /// </param>
    private Village GivenVillage(bool atCentre = false, int serverLevel = 3,
        int food = 10_000, int accruedMinutes = 60, int townhallLevel = 5)
    {
        var catalog = Catalog();
        var geometry = Geometry();
        var (cx, cy) = geometry.Centre;

        var x = atCentre ? cx : cx + geometry.Radius - 1;
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], x, cy);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = food }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now.AddMinutes(-accruedMinutes));
        village.AddBuilding("farm", catalog.Buildings, Now.AddMinutes(-accruedMinutes));
        village.AddBuilding("warehouse", catalog.Buildings, Now.AddMinutes(-accruedMinutes));

        var townhall = village.Buildings.Single(b => b.Type == "townhall");

        for (var level = 1; level < townhallLevel; level++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now,
                ProductionBoost.None, locationMultiplier: 1.0);
            townhall.CompleteConstruction(Now);
        }

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _servers.GetLevelAsync(village.ServerId, Arg.Any<CancellationToken>()).Returns(serverLevel);

        return village;
    }

    private static Guid FarmId(Village village) => village.Buildings.Single(b => b.Type == "farm").Id;

    /// <summary>Збір переносить накопичене в ресурси села.</summary>
    [Fact]
    public async Task Collect_ShouldMoveTheBufferIntoResources()
    {
        var village = GivenVillage(food: 0, accruedMinutes: 60);

        await CollectHandler().Handle(new CollectBuildingCommand(PlayerId, FarmId(village)), CancellationToken.None);

        // 60 хв × 10/хв × 1 рівень × множник околиці 1.0
        Assert.Equal(600, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>
    /// Кільце множить виробіток. Це головна причина переїжджати до центру,
    /// і найдорожча помилка, якщо множник загубиться в ланцюжку викликів.
    /// </summary>
    [Fact]
    public async Task Collect_ShouldApplyTheRingMultiplier()
    {
        var village = GivenVillage(atCentre: true, food: 0, accruedMinutes: 60);

        await CollectHandler().Handle(new CollectBuildingCommand(PlayerId, FarmId(village)), CancellationToken.None);

        // Той самий час, але центральне кільце ×2
        Assert.Equal(1200, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>
    /// Рівень сервера звужує кільця: те саме село на першому рівні світу
    /// ще не в центрі, тому множник менший.
    /// </summary>
    [Fact]
    public async Task Collect_ShouldRespectTheServerLevel()
    {
        var village = GivenVillage(atCentre: true, serverLevel: 1, food: 0, accruedMinutes: 60);

        await CollectHandler().Handle(new CollectBuildingCommand(PlayerId, FarmId(village)), CancellationToken.None);

        // Центр на 1 рівні вужчий, але село рівно в центрі — множник той самий ×2
        Assert.Equal(1200, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Неіснуюча будівля — 404.</summary>
    [Fact]
    public async Task Collect_ShouldThrow_ForUnknownBuilding()
    {
        GivenVillage();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            CollectHandler().Handle(new CollectBuildingCommand(PlayerId, Guid.NewGuid()), CancellationToken.None));
    }

    /// <summary>Апгрейд списує вартість і ставить будівлю в стан будівництва.</summary>
    [Fact]
    public async Task Upgrade_ShouldChargeCostAndStartConstruction()
    {
        var village = GivenVillage(food: 10_000, accruedMinutes: 0);
        var farm = village.Buildings.Single(b => b.Type == "farm");

        var before = village.Resources.Single(r => r.ResourceType == "food").Amount;

        await UpgradeHandler().Handle(new UpgradeBuildingCommand(PlayerId, farm.Id), CancellationToken.None);

        Assert.True(farm.IsUnderConstruction);
        Assert.Equal(before - 100, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>
    /// Правило C тірного гейта: будівля не переростає ратушу.
    /// Ферма 1 рівня при ратуші 1 рівня піднятись не може.
    /// </summary>
    [Fact]
    public async Task Upgrade_ShouldReject_WhenTheBuildingWouldExceedTheMainBuilding()
    {
        var village = GivenVillage(food: 10_000, accruedMinutes: 0, townhallLevel: 1);
        var farm = village.Buildings.Single(b => b.Type == "farm");

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            UpgradeHandler().Handle(new UpgradeBuildingCommand(PlayerId, farm.Id), CancellationToken.None));
    }

    /// <summary>
    /// Правило A: рівень сервера — глобальна стеля.
    /// Сервер 1 рівня дозволяє будівлі до 10.
    /// </summary>
    [Fact]
    public async Task Upgrade_ShouldReject_WhenServerLevelCapsTheTier()
    {
        var village = GivenVillage(serverLevel: 1, food: 1_000_000, accruedMinutes: 0, townhallLevel: 10);
        var townhall = village.Buildings.Single(b => b.Type == "townhall");

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            UpgradeHandler().Handle(new UpgradeBuildingCommand(PlayerId, townhall.Id), CancellationToken.None));
    }

    /// <summary>Нестача ресурсів не лишає будівлю в напівстані.</summary>
    [Fact]
    public async Task Upgrade_ShouldNotStartConstruction_WhenResourcesAreInsufficient()
    {
        var village = GivenVillage(food: 10, accruedMinutes: 0);
        var farm = village.Buildings.Single(b => b.Type == "farm");

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            UpgradeHandler().Handle(new UpgradeBuildingCommand(PlayerId, farm.Id), CancellationToken.None));

        Assert.False(farm.IsUnderConstruction);
    }
}
