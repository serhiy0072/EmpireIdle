using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Inventory.Contracts;
using EmpireIdle.Application.Inventory.Effects;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Inventory;

/// <summary>
/// Чотири типи телепорта (GDD §8.9): точний, ближній у радіусі, клановий на своїй
/// території й випадковий. Межа типу перевіряється до спільних правил заселення.
/// </summary>
public class TeleportItemEffectTests
{
    private static readonly DateTime Now = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Map = new MapConfig
        {
            Width = 200,
            Height = 200,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }],
            Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.5],
                RingMultipliers = [1.0, 1.0],
                RingsAtFirstLevel = 1.0,
                FogMinShare = 1.0,
                FogMaxShare = 1.0
            }
        },
        Clan = new ClanConfig { Territory = new ClanTerritoryConfig { Enabled = true, Radius = 5 } }
    };

    private TeleportItemEffect Effect()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var terrain = new TerrainGenerator(config.Map);
        var geometry = new WorldGeometry(config.Map);
        var calculator = new MarchCalculator(terrain, catalog);

        _serverContext.ServerId.Returns(1);
        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _heroes.GetByGarrisonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Hero>());
        _heroes.GetForeignGarrisonIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _garrisons.GetHoldingReinforcementsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Garrison>());

        var logistics = new MarchLogistics(_villages, _heroes, catalog, calculator,
            new HeroProgression(config.HeroSettings), NullLogger<MarchLogistics>.Instance);
        var returner = new ReinforcementReturner(_garrisons, _villages, _structures, _marches, _heroes,
            calculator, catalog, new HeroProgression(config.HeroSettings), TestEffects.Resolver(Substitute.For<IActiveEffectRepository>()), NullLogger<ReinforcementReturner>.Instance);
        var relocator = new VillageRelocator(_map, _marches, _garrisons, _heroes, _servers, catalog, geometry,
            TestEffects.Resolver(Substitute.For<IActiveEffectRepository>()),
            new MarchHomecoming(_heroes, logistics, NullLogger<MarchHomecoming>.Instance), returner);

        return new TeleportItemEffect(_villages, _map, _servers, _clans, _serverContext, geometry, terrain,
            new SettlementPlacer(terrain, geometry, new SystemRandomSource()),
            new TerritoryBonus(_clans, _structures, new ClanTerritoryRules(catalog)), relocator);
    }

    private Village GivenVillage()
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Home", ["food"], 100, 100);
        village.AddBuilding("townhall", catalog.Buildings, Now.AddHours(-1));

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(new List<March>());

        return village;
    }

    private static ItemUsageContext Use(TeleportScope scope, int? x, int? y, int range = 0)
        => new(PlayerId, new ItemConfig { Key = "teleport", Type = "teleport", TeleportScope = scope, TeleportRange = range },
            1, Now, x, y);

    [Fact]
    public async Task Exact_ShouldMoveToTheChosenCell()
    {
        var village = GivenVillage();

        await Effect().ApplyAsync(Use(TeleportScope.Exact, 150, 40), CancellationToken.None);

        Assert.Equal((150, 40), (village.X, village.Y));
    }

    [Fact]
    public async Task Nearby_ShouldMoveWithinTheRange()
    {
        var village = GivenVillage();

        await Effect().ApplyAsync(Use(TeleportScope.Nearby, 160, 140, range: 100), CancellationToken.None);

        Assert.Equal((160, 140), (village.X, village.Y));
    }

    /// <summary>Відстань Чебишева, як і скрізь на мапі: 101 клітина по діагоналі — уже задалеко.</summary>
    [Fact]
    public async Task Nearby_ShouldRefuseACellBeyondTheRange()
    {
        var village = GivenVillage();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Effect().ApplyAsync(Use(TeleportScope.Nearby, 199, 199, range: 98), CancellationToken.None));

        Assert.Equal(RefusalReasons.TeleportTooFar.Key, refusal.Reason);
        Assert.Equal(98, refusal.Args["range"]);
        Assert.Equal((100, 100), (village.X, village.Y));
    }

    [Fact]
    public async Task Clan_ShouldRefuse_WithoutAClan()
    {
        GivenVillage();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Effect().ApplyAsync(Use(TeleportScope.ClanTerritory, 120, 120), CancellationToken.None));

        Assert.Equal(RefusalReasons.TeleportNoClan.Key, refusal.Reason);
    }

    [Fact]
    public async Task Clan_ShouldRefuse_ACellOutsideTheTerritory()
    {
        GivenVillage();
        var clanId = Guid.NewGuid();
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(clanId);
        _structures.GetByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(new List<ClanStructure>());

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Effect().ApplyAsync(Use(TeleportScope.ClanTerritory, 120, 120), CancellationToken.None));

        Assert.Equal(RefusalReasons.TeleportOutsideClanTerritory.Key, refusal.Reason);
    }

    /// <summary>Випадковий не бере координат від гравця: клітину обирає гра у відкритій зоні.</summary>
    [Fact]
    public async Task Random_ShouldMoveToAFreeCell_WithoutCoordinates()
    {
        var village = GivenVillage();
        _map.IsOccupiedAsync(1, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        await Effect().ApplyAsync(Use(TeleportScope.Random, null, null), CancellationToken.None);

        Assert.NotEqual((100, 100), (village.X, village.Y));
        Assert.InRange(village.X, 0, 199);
        Assert.InRange(village.Y, 0, 199);
    }

    /// <summary>Клан гравця, де глава — інший гравець із селом у (leaderX, leaderY).</summary>
    private void GivenClanLedBy(Guid leaderId, int leaderX, int leaderY)
    {
        var clan = new Clan(Guid.NewGuid(), 1, "Wolves", "WLF", leaderId, Now.AddDays(-1));
        _clans.GetByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(clan);

        // Глава — сам гравець: його село вже задав GivenVillage
        if (leaderId == PlayerId)
            return;

        clan.Join(PlayerId, 50, Now.AddHours(-1));

        var leaderVillage = new Village(Guid.NewGuid(), leaderId, "Hall", ["food"], leaderX, leaderY);
        _villages.GetByPlayerIdAsync(leaderId, Arg.Any<CancellationToken>()).Returns(leaderVillage);
        _map.GetAreaAsync(1, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MapCell> { new(Guid.NewGuid(), 1, leaderX, leaderY, MapOccupantType.Village, leaderVillage.Id) });
    }

    /// <summary>До лідера — на сусідню з його селом клітину; координати гравець не передає.</summary>
    [Fact]
    public async Task ClanLeader_ShouldMoveNextToTheLeader()
    {
        var village = GivenVillage();
        GivenClanLedBy(Guid.NewGuid(), 140, 60);

        await Effect().ApplyAsync(Use(TeleportScope.ClanLeader, null, null), CancellationToken.None);

        Assert.Equal(1, Math.Max(Math.Abs(village.X - 140), Math.Abs(village.Y - 60)));
    }

    [Fact]
    public async Task ClanLeader_ShouldRefuse_WithoutAClan()
    {
        GivenVillage();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Effect().ApplyAsync(Use(TeleportScope.ClanLeader, null, null), CancellationToken.None));

        Assert.Equal(RefusalReasons.TeleportNoClan.Key, refusal.Reason);
    }

    /// <summary>Глава клану сам до себе не летить — предмет лишається.</summary>
    [Fact]
    public async Task ClanLeader_ShouldRefuse_TheLeaderThemself()
    {
        var village = GivenVillage();
        GivenClanLedBy(PlayerId, 140, 60);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Effect().ApplyAsync(Use(TeleportScope.ClanLeader, null, null), CancellationToken.None));

        Assert.Equal(RefusalReasons.TeleportYouAreLeader.Key, refusal.Reason);
        Assert.Equal((100, 100), (village.X, village.Y));
    }
}
