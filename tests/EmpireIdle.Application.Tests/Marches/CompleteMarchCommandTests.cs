using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
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
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

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
                Stats = new Dictionary<string, double> { ["Attack"] = 10, ["Defense"] = 12 }
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
            RecoveryWindowHours = 24
        },
        Map = new MapConfig
        {
            Width = 100,
            Height = 100,
            TerrainSeed = 1,
            Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
        }
    };

    private CompleteMarchCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var combat = new CombatCalculator(config.Combat, catalog);
        var terrain = new TerrainGenerator(config.Map);

        return new CompleteMarchCommandHandler(
            _marches, _garrisons, _unitOfWork, _map, _monsters, _villages, _reports, _clans,
            catalog, new FakeTimeProvider(Now),
            new MonsterArmyBuilder(catalog),
            terrain,
            new MarchCalculator(terrain, catalog),
            new EffectResolver(_effects),
            new BattleResolver(combat, new CasualtySplitter(config.Combat)),
            NullLogger<CompleteMarchCommandHandler>.Instance);
    }

    /// <summary>Село, гарнізон, марш до монстра — стандартна сцена бою.</summary>
    private (March March, Village Village, Garrison Garrison, Monster Monster) GivenBattle(
        int attackerInfantry = 100, int monsterLevel = 1)
    {
        var catalog = new GameCatalog(Config());

        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);
        village.GrantStartingResources(new Dictionary<string, int> { ["food"] = 0 }, Now);
        village.AddBuilding("townhall", catalog.Buildings, Now);
        village.AddBuilding("hospital", catalog.Buildings, Now);
        village.AddBuilding("warehouse", catalog.Buildings, Now);

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

    /// <summary>Перемога над сильно слабшим монстром прибирає його з карти.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveTheMonster_WhenTheAttackerWins()
    {
        var (march, _, _, monster) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        _monsters.Received(1).Remove(monster);
    }

    /// <summary>Нагорода за перемогу лягає в ресурси села.</summary>
    [Fact]
    public async Task Handle_ShouldGrantRewards_WhenTheAttackerWins()
    {
        var (march, village, _, _) = GivenBattle(attackerInfantry: 500, monsterLevel: 1);

        await Handler().Handle(new CompleteMarchCommand(march.Id), CancellationToken.None);

        Assert.True(village.Resources.Single(r => r.ResourceType == "food").Amount > 0,
            "Перемога має принести нагороду, інакше бій не має сенсу.");
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
}
