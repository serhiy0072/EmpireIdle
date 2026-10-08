using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>
/// Переїзд села (§2.5): марші його не блокують, а всі війська гравця —
/// з маршів, зі здобиччю чи без, і з чужих гарнізонів — одразу вдома.
/// </summary>
public class VillageRelocatorTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly UnitStackKey Infantry = new("infantry", 1);

    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units = [new UnitConfig { Key = "infantry", Stats = new Dictionary<string, double> { ["Speed"] = 4 } }],
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
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
        }
    };

    private VillageRelocator Relocator()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var terrain = new TerrainGenerator(config.Map);
        var calculator = new MarchCalculator(terrain, catalog);
        var effects = TestEffects.Resolver(_effects);

        _heroes.GetByGarrisonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Hero>());
        _heroes.GetForeignGarrisonIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _garrisons.GetHoldingReinforcementsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Garrison>());

        var logistics = new MarchLogistics(_villages, _heroes, catalog, calculator,
            new HeroProgression(config.HeroSettings), NullLogger<MarchLogistics>.Instance);

        var returner = new ReinforcementReturner(
            _garrisons, _villages, Substitute.For<IClanStructureRepository>(), _marches, _heroes,
            calculator, catalog, new HeroProgression(config.HeroSettings),
            TestEffects.Resolver(Substitute.For<IActiveEffectRepository>()), NullLogger<ReinforcementReturner>.Instance);

        return new VillageRelocator(_map, _marches, _garrisons, _heroes, _servers, catalog,
            new WorldGeometry(config.Map), effects,
            new MarchHomecoming(_heroes, logistics, NullLogger<MarchHomecoming>.Instance), returner);
    }

    /// <summary>Село з гарнізоном і маршами, що належать цьому гарнізону.</summary>
    private (Village Village, Garrison Garrison) GivenVillage(params March[] marches)
    {
        var catalog = new GameCatalog(Config());
        var village = new Village(Guid.NewGuid(), PlayerId, "Home", ["food"], 10, 10);
        village.AddBuilding("townhall", catalog.Buildings, Now.AddHours(-1));

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(marches.ToList());

        return (village, garrison);
    }

    private March Attack(Guid garrisonId, MarchTargetType targetType, Hero? hero = null, int infantry = 10)
    {
        var march = new March(Guid.NewGuid(), 1, garrisonId, 10, 10, 40, 40, targetType, Guid.NewGuid(),
            new Dictionary<UnitStackKey, int> { [Infantry] = infantry }, Now.AddMinutes(30), Now.AddMinutes(-10),
            MarchIntent.Attack);

        if (hero is not null)
        {
            hero.Deploy(march.Id, Now.AddMinutes(-10));
            _heroes.OnMarch(march.Id, hero);
        }

        return march;
    }

    /// <summary>Армія в дорозі до цілі — одразу вдома, бою не буде, тривогу знято.</summary>
    [Fact]
    public async Task RelocateAsync_ShouldBringAnOutboundAttackHomeAtOnce()
    {
        var (village, garrison) = GivenVillage();
        var march = Attack(garrison.Id, MarchTargetType.Village);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);
        march.ClearDomainEvents();

        await Relocator().RelocateAsync(village, 70, 70, Now, CancellationToken.None);

        Assert.Equal(MarchState.Completed, march.State);
        Assert.Equal(10, garrison.Units.Single(u => u.UnitType == "infantry").Count);
        Assert.Contains(march.DomainEvents, e => e is HostileMarchCalledOff);
        Assert.Equal((70, 70), (village.X, village.Y));
    }

    /// <summary>Здобич не губиться при телепорті: марш, що повертався, розвантажується одразу.</summary>
    [Fact]
    public async Task RelocateAsync_ShouldUnloadTheCargoOfAReturningMarch()
    {
        var (village, garrison) = GivenVillage();
        var march = Attack(garrison.Id, MarchTargetType.Monster);
        march.TurnBack(TimeSpan.FromMinutes(20), Now.AddMinutes(-5));
        march.LoadCargo(new Dictionary<string, int> { ["food"] = 300 }, Now.AddMinutes(-5));
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns([march]);

        await Relocator().RelocateAsync(village, 70, 70, Now, CancellationToken.None);

        Assert.Equal(MarchState.Completed, march.State);
        Assert.Equal(300, village.Resources.Single(r => r.ResourceType == "food").Amount);
        Assert.Equal(10, garrison.Units.Single().Count);
    }

    /// <summary>
    /// Два герої з двох маршів прибувають в одній транзакції: лідером стає лише один,
    /// інакше унікальний індекс лідера зламав би збереження.
    /// </summary>
    [Fact]
    public async Task RelocateAsync_ShouldGiveTheLeaderSlotToOneReturningHeroOnly()
    {
        var (village, garrison) = GivenVillage();

        var first = new Hero(Guid.NewGuid(), PlayerId, 1, "knight", garrison.Id, asLeader: false, Now.AddHours(-1));
        var second = new Hero(Guid.NewGuid(), PlayerId, 1, "archer", garrison.Id, asLeader: false, Now.AddHours(-1));
        var relocator = Relocator();

        // Марші — до Returns: Attack сам налаштовує підміну героїв, а вкладене налаштування NSubstitute не терпить
        List<March> marches =
        [
            Attack(garrison.Id, MarchTargetType.Monster, first),
            Attack(garrison.Id, MarchTargetType.Monster, second)
        ];
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(marches);

        await relocator.RelocateAsync(village, 70, 70, Now, CancellationToken.None);

        Assert.Equal(garrison.Id, first.StationedGarrisonId);
        Assert.Equal(garrison.Id, second.StationedGarrisonId);
        Assert.Single(new[] { first, second }, hero => hero.IsLeader);
    }

    /// <summary>Війська, що стоять підкріпленням у союзника, теж одразу вдома — без маршу.</summary>
    [Fact]
    public async Task RelocateAsync_ShouldBringReinforcementsHomeWithoutAMarch()
    {
        var (village, garrison) = GivenVillage();

        var host = new Garrison(Guid.NewGuid(), Guid.NewGuid(), 1);
        host.AddReinforcements(PlayerId, garrison.Id,
            new Dictionary<UnitStackKey, int> { [Infantry] = 7 }, 100, Now.AddHours(-2));

        var relocator = Relocator();
        _garrisons.GetHoldingReinforcementsAsync(PlayerId, Arg.Any<CancellationToken>()).Returns([host]);

        await relocator.RelocateAsync(village, 70, 70, Now, CancellationToken.None);

        Assert.Equal(0, host.ReinforcementCount);
        Assert.Equal(7, garrison.Units.Single().Count);
        await _marches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
