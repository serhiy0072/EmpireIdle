using EmpireIdle.Application.Beasts.Services;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
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
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IBeastPenRepository _pens = Substitute.For<IBeastPenRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

    private EffectResolver Effects(GameCatalog catalog) => TestEffects.Resolver(_effects, _pens, catalog);

    private static GameConfig Config() => new()
    {
        Buildings =
        [
            new BuildingConfig { Key = "townhall", IsMainBuilding = true },
            new BuildingConfig { Key = "beastpen", BeastCapacityPerLevel = 1 }
        ],
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
            },
            new MonsterConfig
            {
                Key = "bats", MinLevel = 1, MaxLevel = 10, UnitGrowth = 1.5, RewardGrowth = 1.3,
                Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                Rewards = [new ResourceCost { Resource = "food", Amount = 100 }]
            },
            new MonsterConfig
            {
                Key = "boars", MinLevel = 1, MaxLevel = 10, UnitGrowth = 1.5, RewardGrowth = 1.3,
                Units = [new UnitStack { UnitType = "infantry", Count = 1 }],
                Rewards = [new ResourceCost { Resource = "food", Amount = 100 }]
            }
        ],
        Items = [new ItemConfig { Key = "beast_feed", DisplayName = "Корм", Description = "Корм", Type = "feed" }],
        // Кажани не приручаються: звіра для них немає
        Beasts = new BeastsConfig
        {
            PityWins = 10,
            MaxRank = 5,
            LevelsPerRank = 10,
            FeedItemKey = "beast_feed",
            BaseExperience = 100,
            ExperienceGrowth = 1.25,
            Types =
            [
                new BeastConfig
                {
                    Key = "wolf", MonsterKey = "wolves", TameChance = 0.2, Effect = EffectTarget.MarchSpeed,
                    BaseBonus = 0.25, DurationMinutes = 120, CooldownMinutes = 480, ActivationFood = 100
                },
                new BeastConfig
                {
                    Key = "boar", MonsterKey = "boars", TameChance = 0.2, Effect = EffectTarget.Production,
                    BaseBonus = 0.15, DurationMinutes = 120, CooldownMinutes = 480, ActivationFood = 100
                }
            ]
        },
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
            _monsters, _villages, _garrisons, _heroes, new MonsterArmyBuilder(catalog), heroModifiers, catalog, status,
            _structures, _clans, new ClanTerritoryRules(catalog), _marches, Effects(catalog));

        var reinforcementRules = new ReinforcementRules(_clans, _garrisons, catalog, status, capacities);

        return new SendMarchCommandHandler(
            _villages, _garrisons, _marches, _heroes, _unitOfWork, _serverContext,
            new FakeTimeProvider(Now),
            new MarchCalculator(terrain, catalog),
            targets, reinforcementRules, new StructureMarchRules(_clans),
            new HeroProgression(config.HeroSettings),
            catalog,
            new BeastTamer(_pens, _monsters, Substitute.For<IRandomSource>(), new BeastTaming(catalog), capacities, status, catalog),
            Effects(catalog),
            NullLogger<SendMarchCommandHandler>.Instance);
    }

    /// <summary>
    /// Село з гарнізоном, монстр на карті, задана кількість активних маршів
    /// і вільних героїв. Герой повертається назовні, бо кожен другий тест
    /// перевіряє саме його стан.
    /// </summary>
    private (Garrison Garrison, Monster Monster, Hero Hero) GivenState(
        int infantry = 100, int activeMarches = 0, int availableHeroes = 3, bool withPen = false, string monsterType = "wolves")
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);

        if (withPen)
            village.AddBuilding("beastpen", new GameCatalog(Config()).Buildings, Now);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);

        if (infantry > 0)
            garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = infantry }, Now);

        var monster = new Monster(Guid.NewGuid(), 1, monsterType, 1, 55, 55, Now);

        var hero = new Hero(Guid.NewGuid(), PlayerId, 1, "warrior_bran", garrison.Id, asLeader: true, Now);

        var existing = Enumerable.Range(0, activeMarches)
            .Select(_ => new March(
                Guid.NewGuid(), 1, garrison.Id, Guid.NewGuid(), 50, 50, 60, 60,
                MarchTargetType.Monster, Guid.NewGuid(),
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 1 },
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

    private static SendMarchCommand Send(Guid targetId, Guid heroId, int infantry = 10,
        MarchIntent intent = MarchIntent.Attack) =>
        new(PlayerId, MarchTargetType.Monster, targetId,
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = infantry }, heroId, intent);

    /// <summary>Звіринець з одним прирученим звіром — на одне місце це повний звіринець.</summary>
    private void GivenPenWith(string beastKey)
    {
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming(beastKey, rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);

        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);
    }

    /// <summary>Приручати нікуди, доки звіринець не відкритий (GDD §5.10).</summary>
    [Fact]
    public async Task Tame_ShouldRefuse_WithoutABeastPen()
    {
        var (_, monster, hero) = GivenState();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id, intent: MarchIntent.Tame), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastPenMissing.Key, refusal.Reason);
    }

    [Fact]
    public async Task Tame_ShouldRefuse_AMonsterThatGivesNoBeast()
    {
        var (_, monster, hero) = GivenState(withPen: true, monsterType: "bats");

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id, intent: MarchIntent.Tame), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastNotTameable.Key, refusal.Reason);
    }

    /// <summary>Новий вид у повний звіринець — відмова до відправки, а армія лишається вдома.</summary>
    [Fact]
    public async Task Tame_ShouldRefuse_ANewKindWhenThePenIsFull()
    {
        var (garrison, monster, hero) = GivenState(withPen: true);
        GivenPenWith("boar");

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id, intent: MarchIntent.Tame), CancellationToken.None));

        Assert.Equal(RefusalReasons.BeastPenFull.Key, refusal.Reason);
        Assert.Equal(1, refusal.Args["capacity"]);
        Assert.Equal(100, garrison.Units.Sum(u => u.Count));
    }

    /// <summary>Дублікат місця не займає: на вже приручений вид іти можна й з повним звіринцем.</summary>
    [Fact]
    public async Task Tame_ShouldSend_ForAKindAlreadyInAFullPen()
    {
        var (_, monster, hero) = GivenState(withPen: true);
        GivenPenWith("wolf");

        await Handler().Handle(Send(monster.Id, hero.Id, intent: MarchIntent.Tame), CancellationToken.None);

        await _marches.Received(1).AddAsync(Arg.Is<March>(m => m.Intent == MarchIntent.Tame), Arg.Any<CancellationToken>());
    }

    /// <summary>Власне село не атакують: інакше підкріплення соклановців гинули б від господаря.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AnAttackOnTheOwnVillage()
    {
        var (garrison, _, hero) = GivenState(infantry: 100);
        var village = await _villages.GetByPlayerIdAsync(PlayerId);
        _villages.GetByIdAsync(village!.Id, Arg.Any<CancellationToken>()).Returns(village);
        _heroes.GetByGarrisonAsync(garrison.Id, Arg.Any<CancellationToken>()).Returns(new List<Hero>());

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(
            new SendMarchCommand(PlayerId, MarchTargetType.Village, village.Id,
                new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, hero.Id),
            CancellationToken.None));

        Assert.Equal(RefusalReasons.MarchOwnVillage.Key, refusal.Reason);
        Assert.Equal(100, garrison.Units.Sum(u => u.Count));
    }

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

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchCapacity.Key, refusal.Reason);
    }

    /// <summary>Не можна відправити більше, ніж є в гарнізоні.</summary>
    [Fact]
    public async Task Handle_ShouldReject_WhenUnitsAreInsufficient()
    {
        var (garrison, monster, hero) = GivenState(infantry: 5);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id, infantry: 10), CancellationToken.None));
        Assert.Equal(RefusalReasons.GarrisonNotEnoughUnits.Key, refusal.Reason);

        Assert.Equal(5, garrison.Units.Sum(u => u.Count));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Порожня армія — не марш.</summary>
    [Fact]
    public async Task Handle_ShouldReject_AnEmptyArmy()
    {
        var (_, monster, hero) = GivenState();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(
                new SendMarchCommand(PlayerId, MarchTargetType.Monster, monster.Id,
                    new Dictionary<UnitStackKey, int>(), hero.Id),
                CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchEmptyAttack.Key, refusal.Reason);
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

        var second = new Hero(Guid.NewGuid(), PlayerId, 1, "archer_lyra", garrison.Id, asLeader: false, Now);
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

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchHeroUnavailable.Key, refusal.Reason);
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenTheHeroIsWounded()
    {
        var (_, monster, hero) = GivenState();
        hero.Wound(Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchHeroUnavailable.Key, refusal.Reason);
    }

    /// <summary>Чужий герой не відрізняється від неіснуючого.</summary>
    [Fact]
    public async Task Handle_ShouldThrow_WhenTheHeroBelongsToAnotherPlayer()
    {
        var (garrison, monster, _) = GivenState();

        var stranger = new Hero(Guid.NewGuid(), Guid.NewGuid(), 1, "warrior_bran", garrison.Id, asLeader: false, Now);
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

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchCapacity.Key, refusal.Reason);
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
        var (_, monster, stationedHere) = GivenState();

        // Той самий герой, але стоїть в іншому гарнізоні
        var hero = new Hero(stationedHere.Id, PlayerId, 1, "warrior_bran", Guid.NewGuid(), asLeader: false, Now);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(Send(monster.Id, hero.Id), CancellationToken.None));
        Assert.Equal(RefusalReasons.MarchHeroElsewhere.Key, refusal.Reason);
    }

    /// <summary>Ратуша заданого рівня: з рівня 3 спадає щит новачка, без цього підкріплення не шлють.</summary>
    private static void RaiseTownHall(Village village, int level)
    {
        var catalog = new GameCatalog(Config());
        village.AddBuilding("townhall", catalog.Buildings, Now);

        var townhall = village.Buildings.Single();
        for (var i = 1; i < level; i++)
        {
            townhall.BeginUpgrade(catalog.Buildings["townhall"], TimeSpan.Zero, Now, ProductionBoost.None, 1.0);
            townhall.CompleteConstruction(Now);
        }
    }

    /// <summary>
    /// Підкріплення з самого героя юнітів не знімає, але гарнізон зрушує: на його xmin
    /// тримається стеля маршів, і без цього два паралельні відправлення обидва пройшли б перевірку.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldTouchTheGarrison_WhenAHeroReinforcesAlone()
    {
        var (garrison, _, hero) = GivenState(infantry: 0);

        var origin = await _villages.GetByPlayerIdAsync(PlayerId);
        RaiseTownHall(origin!, level: 3);

        var allyId = Guid.NewGuid();
        var allyVillage = new Village(Guid.NewGuid(), allyId, "Ally", ["food"], 52, 52);
        RaiseTownHall(allyVillage, level: 3);
        var allyGarrison = new Garrison(Guid.NewGuid(), allyVillage.Id, 1);

        _villages.GetByIdAsync(allyVillage.Id, Arg.Any<CancellationToken>()).Returns(allyVillage);
        _garrisons.GetByVillageIdAsync(allyVillage.Id, Arg.Any<CancellationToken>()).Returns(allyGarrison);
        _heroes.GetByGarrisonAsync(allyGarrison.Id, Arg.Any<CancellationToken>()).Returns(new List<Hero>());

        var clanId = Guid.NewGuid();
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(clanId);
        _clans.GetClanIdByMemberAsync(allyId, Arg.Any<CancellationToken>()).Returns(clanId);

        await Handler().Handle(
            new SendMarchCommand(PlayerId, MarchTargetType.Village, allyVillage.Id,
                new Dictionary<UnitStackKey, int>(), hero.Id, MarchIntent.Reinforce),
            CancellationToken.None);

        Assert.Equal(Now, garrison.UpdatedAt);
        Assert.Equal(HeroState.Deployed, hero.State);

        await _marches.Received(1).AddAsync(
            Arg.Is<March>(m => m.Intent == MarchIntent.Reinforce && m.HeroId == hero.Id && m.GetUnits().Count == 0),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Активний вовк зі швидкістю +25%: дорога коротша в 1.25 раза, і множник фіксується
    /// на марші — зворотна дорога піде так само швидко.
    /// </summary>
    [Fact]
    public async Task SpeedPassive_ShouldShortenTheMarch_AndStayOnIt()
    {
        var marches = new List<March>();
        await _marches.AddAsync(Arg.Do<March>(marches.Add), Arg.Any<CancellationToken>());

        var (_, plainTarget, plainHero) = GivenState();
        await Handler().Handle(Send(plainTarget.Id, plainHero.Id), CancellationToken.None);

        var (_, fastTarget, fastHero) = GivenState();
        var pen = new BeastPen(Guid.NewGuid(), PlayerId, 1, Now);
        pen.ResolveTaming("wolf", rolled: true, pityWins: 10, capacity: 1, maxRank: 5, Now);
        pen.Activate("wolf", _ => EffectTarget.MarchSpeed, TimeSpan.FromHours(2), TimeSpan.FromHours(8), Now.AddMinutes(-10));
        _pens.GetByPlayerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(pen);

        await Handler().Handle(Send(fastTarget.Id, fastHero.Id), CancellationToken.None);

        var plain = marches[0].ArrivesAt - Now;
        var fast = marches[1].ArrivesAt - Now;

        Assert.Equal(plain.TotalSeconds / 1.25, fast.TotalSeconds, precision: 3);
        Assert.Equal(1.25, marches[1].SpeedMultiplier, precision: 10);
        Assert.Equal(1.0, marches[0].SpeedMultiplier, precision: 10);
    }
}
