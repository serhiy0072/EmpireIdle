using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Найбільший хендлер у проєкті: бій, втрати, нагороди, звіт, розворот.
/// Перевіряємо не формулу бою (вона в BattleResolverTests), а те, що
/// хендлер правильно склеює кроки й не втрачає армію на переходах.
/// </summary>
public class CompleteMarchCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMapRepository _map = Substitute.For<IMapRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IBattleReportRepository _reports = Substitute.For<IBattleReportRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();
    private readonly IServerRepository _serverRepository = Substitute.For<IServerRepository>();

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "hospital", WoundedCapacityPerLevel = 100, UpgradeCostGrowth = 1.45 },
            new BuildingConfig { Key = "warehouse", StoresResources = ["food"], BaseStorage = 100_000,
                UpgradeCostGrowth = 1.45 }
        ],
        Units =
        [
            new UnitConfig
            {
                Key = "infantry",
                Stats = new Dictionary<string, double>
                {
                    ["Attack"] = 10,
                    ["Defense"] = 12,
                    ["CarryCapacity"] = 40
                }
            }
        ],
        Monsters =
        [
            new MonsterConfig
            {
                Key = "wolves", MinLevel = 1, MaxLevel = 10, UnitGrowth = 1.5, RewardGrowth = 1.3,
                Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                Rewards = [new ResourceCost { Resource = "food", Amount = 500 }]
            }
        ],
        Combat = new CombatConfig
        {
            RandomSigma = 0.15,
            RandomMin = 0.7,
            RandomMax = 1.4,
            WoundedShareMin = 0.3,
            WoundedShareMax = 0.5,
            RecoverableShare = 0.2,
            RecoveryWindowHours = 24,

            // Явно, а не на дефолтах: від першого залежить, чи буде бій
            // за село взагалі, від другого — розподіл втрат переможця
            NewbieShieldTownHallLevel = 3,
            NoLossShareThreshold = 0.03
        },
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }],

            // Грабунок матеріалізує буфери, а це вимагає множника кільця.
            // Одне кільце з нейтральним множником: тести не про геометрію
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

    private CompleteMarchCommandHandler Handler(DateTime? at = null)
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var combat = new CombatCalculator(config.Combat, catalog);
        var terrain = new TerrainGenerator(config.Map);
        var geometry = new WorldGeometry(config.Map);
        var calculator = new MarchCalculator(terrain, catalog);
        var casualties = new CasualtySplitter(config.Combat);
        var resolver = new BattleResolver(combat, casualties);
        var effects = new EffectResolver(_effects);

        var logistics = new MarchLogistics(
            _villages, catalog, calculator, NullLogger<MarchLogistics>.Instance);

        var monsterBattle = new MonsterBattleService(
            _monsters, _map, _garrisons, _villages, _reports, _random,
            catalog, new MonsterArmyBuilder(catalog), resolver, effects, logistics,
            NullLogger<MonsterBattleService>.Instance);

        var villageBattle = new VillageBattleService(
            _garrisons, _villages, _reports, _serverRepository, _notifier, _random,
            catalog, resolver, new DefenceLossAllocator(), casualties, effects, geometry, logistics,
            NullLogger<VillageBattleService>.Instance);

        var reinforcements = new ReinforcementDelivery(
            _garrisons, _villages, _clans, catalog, logistics,
            NullLogger<ReinforcementDelivery>.Instance);

        return new CompleteMarchCommandHandler(
            _marches, _garrisons, _unitOfWork, terrain,
            new FakeTimeProvider(at ?? Now),
            logistics, monsterBattle, villageBattle, reinforcements,
            NullLogger<CompleteMarchCommandHandler>.Instance);
    }

    /// <summary>Село з ратушею потрібного рівня — інакше діє щит новачка.</summary>
    private static Village NewVillage(GameCatalog catalog, Guid ownerId, int x, int y, int townHallLevel = 5)
    {
        var village = new Village(Guid.NewGuid(), ownerId, "Test", ["food"], x, y);

        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 0 }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("hospital", catalog.Buildings, Now);
        village.AddBuilding("warehouse", catalog.Buildings, Now);

        var townhall = village.Buildings.Single(b => b.Type == "townhall");

        for (var i = 1; i < townHallLevel; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }

        return village;
    }

    /// <summary>Село, гарнізон, марш до монстра — стандартна сцена бою.</summary>
    private (March March, Village Village, Garrison Garrison, Monster Monster) GivenBattle(
        int attackerInfantry = 100, int monsterLevel = 1)
    {
        var catalog = new GameCatalog(Config());

        var village = NewVillage(catalog, PlayerId, 50, 50);

        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        var monster = new Monster(Guid.NewGuid(), 1, "wolves", monsterLevel, 55, 55, Now);

        var march = new March(
            Guid.NewGuid(), 1, garrison.Id, 50, 50, 55, 55,
            MarchTargetType.Monster, monster.Id,
            new Dictionary<string, int> { ["infantry"] = attackerInfantry },
            Now, Now.AddMinutes(-30));

        _marches.GetByIdAsync(march.Id, Arg.Any<CancellationToken>()).Returns(march);
        _garrisons.GetByIdAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);

        return (march, village, garrison, monster);
    }

    /// <summary>
    /// Сцена PvP: обидва села з ратушею 5, тобто вище порогу щита.
    /// </summary>
    private (March March, Village Attacker, Garrison AttackerGarrison, Village Defender, Garrison DefenderGarrison)
        GivenVillageBattle(int attackerInfantry = 500, int defenderInfantry = 10, int defenderFood = 0)
    {
        var catalog = new GameCatalog(Config());

        var attacker = NewVillage(catalog, PlayerId, 50, 50);
        var defender = NewVillage(catalog, Guid.NewGuid(), 55, 55);

        if (defenderFood > 0)
            defender.GrantResource("food", defenderFood, catalog.Buildings, Now);

        var attackerGarrison = new Garrison(Guid.NewGuid(), attacker.Id, 1);
        var defenderGarrison = new Garrison(Guid.NewGuid(), defender.Id, 1);

        if (defenderInfantry > 0)
            defenderGarrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = defenderInfantry }, Now);

        var march = new March(
            Guid.NewGuid(), 1, attackerGarrison.Id, 50, 50, 55, 55,
            MarchTargetType.Village, defender.Id,
            new Dictionary<string, int> { ["infantry"] = attackerInfantry },
            Now, Now.AddMinutes(-30));

        _marches.GetByIdAsync(march.Id, Arg.Any<CancellationToken>()).Returns(march);
        _garrisons.GetByIdAsync(attackerGarrison.Id, Arg.Any<CancellationToken>()).Returns(attackerGarrison);
        _garrisons.GetByVillageIdAsync(defender.Id, Arg.Any<CancellationToken>()).Returns(defenderGarrison);
        _villages.GetByIdAsync(attacker.Id, Arg.Any<CancellationToken>()).Returns(attacker);
        _villages.GetByIdAsync(defender.Id, Arg.Any<CancellationToken>()).Returns(defender);

        return (march, attacker, attackerGarrison, defender, defenderGarrison);
    }

    // ---------- Загальні правила ----------

    /// <summary>
    /// Завершений марш пропускається мовчки: паралельний прогін сканера
    /// міг його вже обробити, і повторна обробка подвоїла б нагороду.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheMarchIsAlreadyCompleted()
    {
        var (march, _, _, _) = GivenBattle();

        march.TurnBack(TimeSpan.Zero, Now);
        march.Complete(Now);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        await _reports.DidNotReceive().AddAsync(Arg.Any<BattleReport>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Неіснуючий марш — не помилка: рядок міг видалити інший процес.</summary>
    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheMarchIsMissing()
    {
        _marches.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((March?)null);

        await Handler().Handle(new CompleteMarchCommand(Guid.NewGuid()), CancellationToken.None);

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Бій із монстром ----------

    /// <summary>Перемога над сильно слабшим монстром прибирає його з карти.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheMonster_WhenTheAttackerWins()
    {
        var (march, _, _, monster) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        _monsters.Received(1).Remove(monster);
    }

    /// <summary>
    /// Нагорода за перемогу не з'являється миттєво: вона їде з армією
    /// й лягає на склад лише по прибутті — так само, як здобич із набігу.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldLoadRewardsIntoTheMarch_WhenTheAttackerWins()
    {
        var (march, village, _, _) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.NotEmpty(march.GetCargo());
        Assert.Equal(0, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Повернення армії розвантажує здобич на склад.</summary>
    [Fact]
    public async Task Handle_ShouldUnloadCargo_WhenTheMarchReturns()
    {
        var (march, village, _, _) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        // Перший прогін: бій, здобич у вантажі, армія розвертається
        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        var carried = march.GetCargo().Values.Sum();

        // Другий: армія вдома. Час зсунуто, щоб зворотний шлях завершився
        await Handler(Now.AddHours(6)).Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.True(carried > 0, "бій мав дати здобич");
        Assert.Equal(carried, village.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Звіт створюється завжди — і при перемозі, і при поразці.</summary>
    [Fact]
    public async Task Handle_ShouldAlwaysWriteABattleReport()
    {
        var (march, _, _, _) = GivenBattle(attackerInfantry: 1, monsterLevel: 10);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        await _reports.Received(1).AddAsync(Arg.Any<BattleReport>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Після бою армія розвертається, а не завершує похід одразу:
    /// юніти мають дійти додому, і сканер підбере їх наступним проходом.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTurnTheMarchBack_AfterTheBattle()
    {
        var (march, _, _, _) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.Equal(MarchState.Returning, march.State);
    }

    /// <summary>
    /// Армія, що загинула повністю, завершує похід одразу — повертатись нікому.
    /// Інакше марш висів би в Returning вічно.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldCompleteTheMarch_WhenTheWholeArmyDies()
    {
        var (march, _, _, _) = GivenBattle(attackerInfantry: 1, monsterLevel: 10);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.Equal(MarchState.Completed, march.State);
    }

    /// <summary>Марш, що повертається, віддає вцілілих у гарнізон.</summary>
    [Fact]
    public async Task Handle_ShouldReturnSurvivorsToTheGarrison_OnArrival()
    {
        var (march, _, garrison, _) = GivenBattle(attackerInfantry: 10);

        march.TurnBack(TimeSpan.Zero, Now);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.Equal(10, garrison.Units.Sum(u => u.Count));
        Assert.Equal(MarchState.Completed, march.State);
    }

    /// <summary>
    /// Ціль зникла до прибуття — армія повертається без бою.
    /// Монстра міг убити інший гравець, і це штатна ситуація.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTurnBackWithoutBattle_WhenTheTargetIsGone()
    {
        var (march, _, _, _) = GivenBattle();
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.Equal(MarchState.Returning, march.State);
        await _reports.DidNotReceive().AddAsync(Arg.Any<BattleReport>(), Arg.Any<CancellationToken>());
    }

    // ---------- Бій за село ----------

    /// <summary>
    /// Бій за село доходить до обох сторін: два звіти, і захисник
    /// отримує сповіщення. Без цього напад для нього невидимий.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldWriteReportsForBothSides_WhenAttackingAVillage()
    {
        var (march, _, _, defender, _) = GivenVillageBattle();

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        await _reports.Received(2).AddAsync(Arg.Any<BattleReport>(), Arg.Any<CancellationToken>());

        await _notifier.Received(1).NotifyBattleFinishedAsync(
            defender.PlayerId, Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Переможений захисник втрачає юнітів із гарнізону.</summary>
    [Fact]
    public async Task Handle_ShouldApplyDefenderLosses_WhenTheAttackerWins()
    {
        var (march, _, _, _, defenderGarrison) = GivenVillageBattle(
            attackerInfantry: 500, defenderInfantry: 10);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        var left = defenderGarrison.Units.Sum(u => u.Count);

        Assert.True(left < 10, $"захисник мав утратити юнітів, лишилось {left}");
    }

    /// <summary>
    /// Втрати підкріплень адресні: юніти союзника зникають із його стека,
    /// а поранені лягають у ЙОГО госпіталь, не господаря.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTakeLossesFromTheAllyStack_WhenReinforcementsDefend()
    {
        var (march, _, _, _, defenderGarrison) = GivenVillageBattle(
            attackerInfantry: 500, defenderInfantry: 0);

        var allyId = Guid.NewGuid();
        var allyVillage = NewVillage(new GameCatalog(Config()), allyId, 60, 60);
        var allyGarrison = new Garrison(Guid.NewGuid(), allyVillage.Id, 1);

        _garrisons.GetByIdAsync(allyGarrison.Id, Arg.Any<CancellationToken>()).Returns(allyGarrison);
        _garrisons.GetByVillageIdAsync(allyVillage.Id, Arg.Any<CancellationToken>()).Returns(allyGarrison);
        _villages.GetByPlayerIdAsync(allyId, Arg.Any<CancellationToken>()).Returns(allyVillage);

        defenderGarrison.AddReinforcements(allyId, allyGarrison.Id,
            new Dictionary<string, int> { ["infantry"] = 10 }, 100, Now);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.True(defenderGarrison.ReinforcementCount < 10);
        Assert.True(allyGarrison.WoundedCount > 0, "поранені союзника мали піти до нього");
    }

    /// <summary>Здобич із набігу теж їде маршем, а не з'являється в нападника одразу.</summary>
    [Fact]
    public async Task Handle_ShouldLoadPlunderIntoTheMarch_WhenTheAttackerWins()
    {
        var (march, attacker, _, _, _) = GivenVillageBattle(
            attackerInfantry: 500, defenderInfantry: 1, defenderFood: 5000);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.NotEmpty(march.GetCargo());
        Assert.Equal(0, attacker.Resources.Single(r => r.ResourceType == "food").Amount);
    }

    /// <summary>Марш на село під щитом розвертається, бою немає.</summary>
    [Fact]
    public async Task Handle_ShouldTurnBack_WhenTheTargetIsShielded()
    {
        var catalog = new GameCatalog(Config());
        var (march, _, _, defender, defenderGarrison) = GivenVillageBattle(defenderInfantry: 10);

        // Захисник із ратушею 1 — під щитом
        var shielded = NewVillage(catalog, defender.PlayerId, 55, 55, townHallLevel: 1);

        _villages.GetByIdAsync(march.TargetId, Arg.Any<CancellationToken>()).Returns(shielded);
        _garrisons.GetByVillageIdAsync(shielded.Id, Arg.Any<CancellationToken>()).Returns(defenderGarrison);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.Equal(MarchState.Returning, march.State);
        await _reports.DidNotReceive().AddAsync(Arg.Any<BattleReport>(), Arg.Any<CancellationToken>());
        Assert.Equal(10, defenderGarrison.Units.Sum(u => u.Count));
    }
}
