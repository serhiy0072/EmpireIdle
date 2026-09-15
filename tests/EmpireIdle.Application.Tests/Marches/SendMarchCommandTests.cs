using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Відправка армії. Ключове — юніти знімаються з гарнізону й потрапляють
/// у марш рівно один раз: подвоєння чи втрата армії тут коштують гравцю
/// всього війська.
/// </summary>
public class SendMarchCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();

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
        },
        Monsters =
        [
            new MonsterConfig
            {
                Key = "wolves", MinLevel = 1, MaxLevel = 10, UnitGrowth = 1.5, RewardGrowth = 1.3,
                Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                Rewards = [new ResourceCost { Resource = "food", Amount = 500 }]
            }
        ],
        HeroSettings = new HeroesConfig
        {
            MaxMarches = 3
        }
    };

    private SendMarchCommandHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var terrain = new TerrainGenerator(config.Map);

        _serverContext.ServerId.Returns(1);

        var capacities = new VillageCapacities(catalog);
        var status = new VillageStatus(catalog);
        var heroModifiers = new HeroCombatModifiers(catalog);

        var targets = new MarchTargetResolver(
            _monsters, _villages, _garrisons, _heroes, new MonsterArmyBuilder(catalog), heroModifiers, catalog, status);

        var reinforcementRules = new ReinforcementRules(_clans, _garrisons, catalog, status, capacities);

        return new SendMarchCommandHandler(
            _villages, _garrisons, _marches, _heroes, _unitOfWork, _serverContext,
            new FakeTimeProvider(Now),
            new MarchCalculator(terrain, catalog),
            targets, reinforcementRules,
            new HeroProgression(config.HeroSettings),
            NullLogger<SendMarchCommandHandler>.Instance);
    }

    /// <summary>
    /// Село з гарнізоном, монстр на карті, задана кількість активних маршів
    /// і вільних героїв. Герой повертається назовні, бо кожен другий тест
    /// перевіряє саме його стан.
    /// </summary>
    private (Garrison Garrison, Monster Monster, Hero Hero) GivenState(
        int infantry = 100, int activeMarches = 0, int availableHeroes = 3)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (infantry > 0)
            garrison.ReceiveUnits(new Dictionary<string, int> { ["infantry"] = infantry }, Now);

        var monster = new Monster(Guid.NewGuid(), 1, "wolves", 1, 55, 55, Now);

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, "warrior_bran", Guid.NewGuid(), asLeader: true, Now);
        hero.StationIn(garrison.Id, asLeader: true, Now);

        var existing = Enumerable.Range(0, activeMarches)
            .Select(_ => new March(
                Guid.NewGuid(), 1, garrison.Id, Guid.NewGuid(), 50, 50, 60, 60,
                MarchTargetType.Monster, Guid.NewGuid(),
                new Dictionary<string, int> { ["infantry"] = 1 },
                Now.AddHours(1), Now))
            .ToList();

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _marches.GetActiveByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        _heroes.CountAvailableAsync(PlayerId, garrison.Id, Arg.Any<CancellationToken>()).Returns(availableHeroes);

        return (garrison, monster, hero);
    }

    private static SendMarchCommand Send(Guid targetId, Guid heroId, int infantry = 10) =>
        new(PlayerId, MarchTargetType.Monster, targetId,
            new Dictionary<string, int> { ["infantry"] = infantry }, heroId);

    /// <summary>Юніти зникають із гарнізону — армія не може бути у двох місцях.</summary>
    [Fact]
    public async Task Handle_ShouldRemoveUnitsFromTheGarrison()
    {
        var (garrison, monster, hero) = GivenState(infantry: 100);

        await Handler().Handle(Send(monster.Id, hero.Id, infantry: 30), CancellationToken.None);

        Assert.Equal(70, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Марш зберігається зі складом армії й часом прибуття в майбутньому.</summary>
    [Fact]
    public async Task Handle_ShouldPersistTheMarchWithArrivalInTheFuture()
    {
        var (_, monster, hero) = GivenState();

        await Handler().Handle(Send(monster.Id, hero.Id, infantry: 10), CancellationToken.None);

        await _marches.Received(1).AddAsync(
            Arg.Is<March>(m => m.ArrivesAt > Now
                               && m.DepartedAt == Now
                               && m.TargetX == 55 && m.TargetY == 55),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Ліміт одночасних походів. Без нього гравець розсилав би армію
    /// по одному юніту на кожну ціль карти.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTooManyMarchesAreActive()
    {
        var (_, monster, hero) = GivenState(activeMarches: 3, availableHeroes: 5);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
    }

    /// <summary>Не можна відправити більше, ніж є в гарнізоні.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenUnitsAreInsufficient()
    {
        var (garrison, monster, hero) = GivenState(infantry: 5);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id, infantry: 10), CancellationToken.None));

        Assert.Equal(5, garrison.Units.Sum(u => u.Count));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Порожня армія — не марш.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AnEmptyArmy()
    {
        var (_, monster, hero) = GivenState();

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(
                new SendMarchCommand(PlayerId, MarchTargetType.Monster, monster.Id,
                    new Dictionary<string, int>(), hero.Id),
                CancellationToken.None));
    }

    /// <summary>
    /// Зниклої цілі не буває: монстра міг убити інший гравець між показом
    /// карти й натисканням кнопки — це 404, а не 500.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenTheTargetIsGone()
    {
        var (_, _, hero) = GivenState();
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Send(Guid.NewGuid(), hero.Id), CancellationToken.None));
    }

    /// <summary>
    /// Ціль зникла — гарнізон недоторканий. Юніти знімаються ПІСЛЯ резолву,
    /// інакше невдала відправка з'їдала б армію.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldNotTouchTheGarrison_WhenTheTargetIsGone()
    {
        var (garrison, _, hero) = GivenState(infantry: 100);
        _monsters.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Monster?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Send(Guid.NewGuid(), hero.Id), CancellationToken.None));

        Assert.Equal(100, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Далі ціль — довший шлях.</summary>
    [Fact]
    public async Task Handle_ShouldScaleTravelTimeWithDistance()
    {
        var (garrison, near, hero) = GivenState();

        var second = new Hero(Guid.NewGuid(), PlayerId, 1, "archer_lyra", Guid.NewGuid(), asLeader: false, Now);
        second.StationIn(garrison.Id, asLeader: false, Now);
        _heroes.GetByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);

        var far = new Monster(Guid.NewGuid(), 1, "wolves", 1, 90, 90, Now);
        _monsters.GetByIdAsync(far.Id, Arg.Any<CancellationToken>()).Returns(far);

        await Handler().Handle(Send(near.Id, hero.Id), CancellationToken.None);
        await Handler().Handle(Send(far.Id, second.Id), CancellationToken.None);

        var captured = _marches.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IMarchRepository.AddAsync))
            .Select(c => (March)c.GetArguments()[0]!)
            .ToList();

        Assert.Equal(2, captured.Count);
        Assert.True(captured[1].ArrivesAt > captured[0].ArrivesAt,
            "Дальша ціль має вимагати більше часу.");
    }

    /// <summary>
    /// Вільних героїв немає: єдиний уже в поході. Кап тут ні до чого —
    /// відмову дає стан героя, бо похід веде рівно один герой, і зайнятий
    /// герой і є вичерпаним слотом.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenNoHeroIsFree()
    {
        var (_, monster, hero) = GivenState(activeMarches: 1, availableHeroes: 0);
        hero.Deploy(Now);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenTheHeroIsWounded()
    {
        var (_, monster, hero) = GivenState();
        hero.Wound(Now);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
    }

    /// <summary>Чужий герой не відрізняється від неіснуючого.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenTheHeroBelongsToAnotherPlayer()
    {
        var (garrison, monster, _) = GivenState();

        var stranger = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, "warrior_bran", Guid.NewGuid(), asLeader: false, Now);
        stranger.StationIn(garrison.Id, asLeader: false, Now);
        _heroes.GetByIdAsync(stranger.Id, Arg.Any<CancellationToken>()).Returns(stranger);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(Send(monster.Id, stranger.Id), CancellationToken.None));
    }

    /// <summary>Герой знімається з гарнізону разом із юнітами.</summary>
    [Fact]
    public async Task Handle_ShouldDeployTheHero()
    {
        var (_, monster, hero) = GivenState();

        await Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None);

        Assert.Equal(HeroState.Deployed, hero.State);
        Assert.Null(hero.StationedGarrisonId);
        Assert.False(hero.IsLeader);
    }

    /// <summary>Стеля MaxMarches діє навіть при повному ростері.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheConfigCeilingIsReached()
    {
        var (_, monster, hero) = GivenState(activeMarches: 3, availableHeroes: 5);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
    }

    /// <summary>Три герої дають три походи, а не два.</summary>
    [Fact]
    public async Task Handle_ShouldAllowTheLastHero_WhenTwoAreAlreadyMarching()
    {
        var (_, monster, hero) = GivenState(activeMarches: 2, availableHeroes: 1);

        await Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None);

        Assert.Equal(HeroState.Deployed, hero.State);
    }

    /// <summary>Герой, що стоїть підкріпленням у союзника, з дому не виступає.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenTheHeroIsStationedElsewhere()
    {
        var (_, monster, hero) = GivenState();
        hero.StationIn(Guid.NewGuid(), asLeader: false, Now);

        await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
    }
}
