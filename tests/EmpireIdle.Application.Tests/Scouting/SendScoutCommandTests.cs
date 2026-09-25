using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Scouting.Commands;
using EmpireIdle.Application.Scouting.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Scouting;

/// <summary>
/// Відправка розвідників: досить вежі розвідки, ні героя, ні юнітів. Свого не розвідують,
/// завіса зупиняє ще до виходу, а йдуть розвідники в рази швидше за військо.
/// </summary>
public class SendScoutCommandTests
{
    private const string Tower = "scouttower";
    /// <summary>Утричі швидше за найшвидший юніт TestKit (кіннота, 10).</summary>
    private const double Speed = 30.0;

    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly GameCatalog _catalog;
    private readonly MarchCalculator _calculator;

    public SendScoutCommandTests()
    {
        var config = new GameConfigBuilder().WithBuildings(Tower).WithUnits().WithMap()
            .WithCombat(c => c.Scouting = new() { RequiredBuilding = Tower, Speed = Speed })
            .Build();

        _catalog = new GameCatalog(config);
        _calculator = new MarchCalculator(new TerrainGenerator(config.Map), _catalog);
        _serverContext.ServerId.Returns(1);
        _heroes.GetByGarrisonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Hero>());
    }

    private SendScoutCommandHandler Handler()
    {
        var status = new VillageStatus(_catalog);
        var territory = new ClanTerritoryRules(_catalog);

        var targets = new MarchTargetResolver(Substitute.For<IMonsterRepository>(), _villages, _garrisons,
            _heroes, new MonsterArmyBuilder(_catalog), new HeroCombatModifiers(_catalog),
            _catalog, status, _structures, _clans, territory);

        return new SendScoutCommandHandler(_villages, _garrisons, _marches, _clans, Substitute.For<IUnitOfWork>(),
            _serverContext, new FakeTimeProvider(Now), _calculator, targets, new ScoutVisibility(_effects), status,
            _catalog, NullLogger<SendScoutCommandHandler>.Instance);
    }

    private Village GivenVillage(Guid ownerId, int x, int y, bool withTower)
    {
        var village = new Village(Guid.NewGuid(), ownerId, $"Village {x}", TestKeys.AllResources, x, y);

        village.AddBuilding(TestKeys.Townhall, _catalog.Buildings, Now);
        if (withTower)
            village.AddBuilding(Tower, _catalog.Buildings, Now);

        _villages.GetByPlayerIdAsync(ownerId, Arg.Any<CancellationToken>()).Returns(village);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>())
            .Returns(new Garrison(Guid.NewGuid(), village.Id, 1));

        return village;
    }

    [Fact]
    public async Task Handle_ShouldSendAFastScoutMarch_WithoutHeroOrUnits()
    {
        var home = GivenVillage(PlayerId, 10, 10, withTower: true);
        var target = GivenVillage(Guid.NewGuid(), 30, 10, withTower: false);
        March? sent = null;
        await _marches.AddAsync(Arg.Do<March>(m => sent = m), Arg.Any<CancellationToken>());

        await Handler().Handle(new SendScoutCommand(PlayerId, MarchTargetType.Village, target.Id), CancellationToken.None);

        Assert.NotNull(sent);
        Assert.Equal(MarchIntent.Scout, sent.Intent);
        Assert.Null(sent.HeroId);
        Assert.Empty(sent.Units);

        var cavalry = _calculator.CalculateDuration(1, home.X, home.Y, target.X, target.Y,
            new Dictionary<UnitStackKey, int> { [new UnitStackKey(TestKeys.Cavalry, 1)] = 1 });

        // Кіннота 10 кліток/хв, розвідники 30 — утричі швидші за найшвидше військо
        Assert.InRange(sent.ArrivesAt - Now, cavalry / 3 - TimeSpan.FromMilliseconds(1), cavalry / 3 + TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WithoutAScoutTower()
    {
        GivenVillage(PlayerId, 10, 10, withTower: false);
        var target = GivenVillage(Guid.NewGuid(), 30, 10, withTower: false);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SendScoutCommand(PlayerId, MarchTargetType.Village, target.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.BuildingRequired.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ToScoutTheOwnVillage()
    {
        var home = GivenVillage(PlayerId, 10, 10, withTower: true);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SendScoutCommand(PlayerId, MarchTargetType.Village, home.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.ScoutOwnTarget.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ToScoutTheOwnClanStructure()
    {
        GivenVillage(PlayerId, 10, 10, withTower: true);
        var clanId = Guid.NewGuid();
        var structure = new ClanStructure(Guid.NewGuid(), 1, clanId, 20, 20, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero, Now);

        _structures.GetByIdAsync(structure.Id, Arg.Any<CancellationToken>()).Returns(structure);
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(clanId);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new SendScoutCommand(PlayerId, MarchTargetType.ClanStructure, structure.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.ScoutOwnTarget.Key, refusal.Reason);
    }

    /// <summary>Завіса діє вже зараз — розвідники не йдуть, а строк завіси не розкривається.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheTargetIsVeiled()
    {
        GivenVillage(PlayerId, 10, 10, withTower: true);
        var owner = Guid.NewGuid();
        var target = GivenVillage(owner, 30, 10, withTower: false);

        _effects.GetAsync(owner, EffectTarget.ScoutBlock, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), owner, EffectTarget.ScoutBlock, 1.0, Now, Now.AddHours(24), "scout_veil_24h"));

        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new SendScoutCommand(PlayerId, MarchTargetType.Village, target.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.ScoutBlocked.Key, refusal.Reason);
        Assert.Empty(refusal.Args);
        await _marches.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }
}
