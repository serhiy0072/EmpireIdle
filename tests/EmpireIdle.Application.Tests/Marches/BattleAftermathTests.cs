using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Розбір бою в частині союзників. Ключове правило: підставою для розпуску
/// контингентів є сама поразка, а не втрати. Стек, що вистояв без жодного
/// загиблого, іде додому так само, як і побитий, бо село, яке впало,
/// більше не тримає чужого війська.
/// </summary>
public class BattleAftermathTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Guid HostPlayerId = Guid.NewGuid();
    private static readonly Guid AllyPlayerId = Guid.NewGuid();

    private readonly IBattleReportRepository _reports = Substitute.For<IBattleReportRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();

    private Village _allyVillage = null!;

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units = [new UnitConfig { Key = "infantry", Stats = new Dictionary<string, double> { ["Speed"] = 4 } }],
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
        }
    };

    private BattleAftermath Aftermath()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var calculator = new MarchCalculator(new TerrainGenerator(config.Map), catalog);
        var capacities = new VillageCapacities(catalog);

        var logistics = new MarchLogistics(
            _villages, catalog, calculator, capacities, NullLogger<MarchLogistics>.Instance);

        var returner = new ReinforcementReturner(
            _garrisons, _villages, _marches, _heroes, calculator, catalog,
            new HeroProgression(config.HeroSettings),
            NullLogger<ReinforcementReturner>.Instance);

        return new BattleAftermath(
            _reports, _garrisons, _villages, _heroes, _notifier,
            new CasualtySplitter(config.Combat), catalog, logistics, new VillageStatus(catalog),
            returner, NullLogger<BattleAftermath>.Instance);
    }

    /// <summary>
    /// Село господаря з гарнізоном, село союзника з гарнізоном, і контингент
    /// союзника, що стоїть у господаря. Лідера союзника повертаємо назовні:
    /// половина перевірок дивиться саме на його стан.
    /// </summary>
    private (Garrison Host, Hero AllyLeader) GivenAlliedContingent(int reinforcements = 50)
    {
        var hostVillage = new Village(Guid.NewGuid(), HostPlayerId, "Host", ["food"], 50, 50);
        var hostGarrison = new Garrison(Guid.NewGuid(), hostVillage.Id, 1);

        var allyVillage = new Village(Guid.NewGuid(), AllyPlayerId, "Ally", ["food"], 60, 60);
        _allyVillage = allyVillage;
        var allyGarrison = new Garrison(Guid.NewGuid(), allyVillage.Id, 1);

        if (reinforcements > 0)
            hostGarrison.AddReinforcements(AllyPlayerId, allyGarrison.Id,
                new Dictionary<string, int> { ["infantry"] = reinforcements }, capacity: 1000, Now);

        var hostLeader = new Hero(Guid.NewGuid(), HostPlayerId, 1, "warrior_bran",
            hostGarrison.Id, asLeader: true, Now);

        var allyLeader = new Hero(Guid.NewGuid(), AllyPlayerId, 1, "archer_lyra",
            hostGarrison.Id, asLeader: true, Now);

        _villages.GetByIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(hostVillage);
        _villages.GetByIdAsync(allyVillage.Id, Arg.Any<CancellationToken>()).Returns(allyVillage);
        _villages.GetByPlayerIdAsync(AllyPlayerId, Arg.Any<CancellationToken>()).Returns(allyVillage);
        _villages.GetByPlayerIdAsync(HostPlayerId, Arg.Any<CancellationToken>()).Returns(hostVillage);

        _garrisons.GetByIdAsync(allyGarrison.Id, Arg.Any<CancellationToken>()).Returns(allyGarrison);
        _garrisons.GetByVillageIdAsync(allyVillage.Id, Arg.Any<CancellationToken>()).Returns(allyGarrison);
        _garrisons.GetByVillageIdAsync(hostVillage.Id, Arg.Any<CancellationToken>()).Returns(hostGarrison);

        _heroes.GetByGarrisonAsync(hostGarrison.Id, Arg.Any<CancellationToken>())
            .Returns([hostLeader, allyLeader]);

        return (hostGarrison, allyLeader);
    }

    private static List<StackLoss> Losses(int lost)
        => [new StackLoss(AllyPlayerId, "infantry", lost)];

    // ---------- Оборона вистояла ----------

    [Fact]
    public async Task AdmitAlliedWounded_ShouldKeepReinforcements_WhenDefenceHolds()
    {
        var (host, allyLeader) = GivenAlliedContingent();

        await Aftermath().AdmitAlliedWoundedAsync(
            Losses(10), host, HostPlayerId, defenceLost: false, seed: 1, Now, CancellationToken.None);

        Assert.Equal(50, host.ReinforcementCount);
        Assert.Equal(HeroState.Idle, allyLeader.State);
        Assert.Equal(host.Id, allyLeader.StationedGarrisonId);
        await _marches.DidNotReceive().AddAsync(Arg.Any<March>(), Arg.Any<CancellationToken>());
    }

    // ---------- Оборона впала ----------

    [Fact]
    public async Task AdmitAlliedWounded_ShouldSendAlliesHome_WhenDefenceFalls()
    {
        var (host, allyLeader) = GivenAlliedContingent();

        await Aftermath().AdmitAlliedWoundedAsync(
            Losses(10), host, HostPlayerId, defenceLost: true, seed: 1, Now, CancellationToken.None);

        Assert.Equal(HeroState.Wounded, allyLeader.State);
        Assert.Null(allyLeader.StationedGarrisonId);
        Assert.False(allyLeader.IsLeader);
        Assert.Equal(0, host.ReinforcementCount);
        await _marches.Received(1).AddAsync(Arg.Any<March>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Контингент не втратив нікого, але село впало. Підстава для розпуску —
    /// поразка, тому додому він іде так само.
    /// </summary>
    [Fact]
    public async Task AdmitAlliedWounded_ShouldSendUntouchedContingentHome()
    {
        var (host, allyLeader) = GivenAlliedContingent();

        await Aftermath().AdmitAlliedWoundedAsync(
            [], host, HostPlayerId, defenceLost: true, seed: 1, Now, CancellationToken.None);

        Assert.Equal(HeroState.Wounded, allyLeader.State);
        Assert.Equal(0, host.ReinforcementCount);
        await _marches.Received(1).AddAsync(Arg.Any<March>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Герой союзника стояв без жодного юніта. Марш додому створюється
    /// однаково: інакше герой лишився б у чужому селі назавжди.
    /// </summary>
    [Fact]
    public async Task AdmitAlliedWounded_ShouldSendALoneHeroHome()
    {
        var (host, allyLeader) = GivenAlliedContingent(reinforcements: 0);

        await Aftermath().AdmitAlliedWoundedAsync(
            [], host, HostPlayerId, defenceLost: true, seed: 1, Now, CancellationToken.None);

        Assert.Null(allyLeader.StationedGarrisonId);
        await _marches.Received(1).AddAsync(Arg.Any<March>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Господар не розпускається власним же розбором: його юніти вдома,
    /// а лідера ранить RecordDefenderAsync, не цей метод.
    /// </summary>
    [Fact]
    public async Task AdmitAlliedWounded_ShouldNotTouchTheHost()
    {
        var (host, _) = GivenAlliedContingent();

        var hostLeader = (await _heroes.GetByGarrisonAsync(host.Id, CancellationToken.None))
            .Single(h => h.PlayerId == HostPlayerId);

        await Aftermath().AdmitAlliedWoundedAsync(
            Losses(10), host, HostPlayerId, defenceLost: true, seed: 1, Now, CancellationToken.None);

        Assert.Equal(HeroState.Idle, hostLeader.State);
        Assert.True(hostLeader.IsLeader);
        Assert.Equal(host.Id, hostLeader.StationedGarrisonId);
    }

    /// <summary>
    /// Дому союзника більше немає. Марш не створюється, військо зникає,
    /// а розбір бою не падає: це прогін сканера, і виняток забрав би
    /// з собою весь пакет маршів.
    ///
    /// Герой при цьому лишається стояти пораненим у чужому селі: знищити
    /// героя не можна, а дівати його нікуди, поки в гравця немає села.
    /// </summary>
    [Fact]
    public async Task AdmitAlliedWounded_ShouldSurvive_WhenTheAllyHasNoHome()
    {
        var (host, allyLeader) = GivenAlliedContingent();

        _villages.GetByPlayerIdAsync(AllyPlayerId, Arg.Any<CancellationToken>()).Returns((Village?)null);
        _villages.GetByIdAsync(_allyVillage.Id, Arg.Any<CancellationToken>()).Returns((Village?)null);

        await Aftermath().AdmitAlliedWoundedAsync(
            Losses(10), host, HostPlayerId, defenceLost: true, seed: 1, Now, CancellationToken.None);

        await _marches.DidNotReceive().AddAsync(Arg.Any<March>(), Arg.Any<CancellationToken>());
        Assert.Equal(0, host.ReinforcementCount);
        Assert.Equal(host.Id, allyLeader.StationedGarrisonId);
        Assert.Equal(HeroState.Wounded, allyLeader.State);
    }
}
