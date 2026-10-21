using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Прев'ю бою нічого не змінює, але й не має розкривати чуже:
/// герой рахується лише тоді, коли належить гравцю.
/// </summary>
public class GetBattlePreviewQueryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IMonsterRepository _monsters = Substitute.For<IMonsterRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();

    /// <summary>Спорядження в сценах немає: бійця дають власні стати героя.</summary>
    private IInventoryRepository EmptyInventory()
    {
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem>());
        return _inventory;
    }

    // Герой без власної швидкості ходить зі швидкістю за замовчуванням —
    // утричі повільніше за піхоту, тож його присутність видно в часі дороги
    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
        Units = [new UnitConfig { Key = "infantry", Stats = new Dictionary<string, double> { ["Speed"] = 3 } }],
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
        HeroSettings = new HeroesConfig { DefaultMarchSpeed = 1 }
    };

    private GetBattlePreviewQueryHandler Handler()
    {
        var config = Config();
        var catalog = new GameCatalog(config);
        var terrain = new TerrainGenerator(config.Map);
        var heroModifiers = new HeroCombatModifiers(catalog);

        _serverContext.ServerId.Returns(1);

        var targets = new MarchTargetResolver(
            _monsters, _villages, _garrisons, _heroes, new MonsterArmyBuilder(catalog), heroModifiers, TestCombatants.WithoutGear(catalog), TestCombatants.Defenders(_heroes, catalog), catalog,
            new VillageStatus(catalog), Substitute.For<IClanStructureRepository>(), Substitute.For<IClanRepository>(),
            new ClanTerritoryRules(catalog), Substitute.For<IMarchRepository>(), TestEffects.Resolver(_effects));

        return new GetBattlePreviewQueryHandler(
            _villages, _garrisons, _heroes, _serverContext,
            new CombatCalculator(config.Combat, catalog), terrain, new MarchCalculator(terrain, catalog),
            TestEffects.Resolver(_effects), new FakeTimeProvider(Now), targets, heroModifiers, catalog,
            new HeroProgression(config.HeroSettings),
            new HeroCombatants(EmptyInventory(), new HeroStats(new HeroProgression(config.HeroSettings), catalog), catalog));
    }

    /// <summary>Село з піхотою, монстр поруч; повертає монстра й героя з заданим власником.</summary>
    private (Monster Monster, Hero Hero) GivenState(Guid heroOwnerId)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, Now);

        var monster = new Monster(Guid.NewGuid(), 1, "wolves", 1, 55, 55, Now);
        var hero = new Hero(Guid.NewGuid(), heroOwnerId, 1, "warrior_bran", Guid.NewGuid(), Now);

        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdReadOnlyAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return (monster, hero);
    }

    private Task<BattlePreviewResult> Preview(Guid monsterId, Guid heroId) => Handler().Handle(
        new GetBattlePreviewQuery(PlayerId, MarchTargetType.Monster, monsterId, [heroId],
            new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 5 }),
        CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldCountOwnHero()
    {
        var (monster, hero) = GivenState(heroOwnerId: PlayerId);

        var withHero = await Preview(monster.Id, hero.Id);
        var withoutHero = await Preview(monster.Id, Guid.NewGuid());

        Assert.True(withHero.TravelTime > withoutHero.TravelTime);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreSomeoneElsesHero()
    {
        var (monster, foreignHero) = GivenState(heroOwnerId: Guid.NewGuid());

        var withForeignHero = await Preview(monster.Id, foreignHero.Id);
        var withoutHero = await Preview(monster.Id, Guid.NewGuid());

        Assert.Equal(withoutHero.TravelTime, withForeignHero.TravelTime);
    }

    /// <summary>Проти монстра — вердикт, шанс і умовні сили обох сторін (рішення 12.10.2026).</summary>
    [Fact]
    public async Task Handle_ShouldShowPowersAndAVerdict_AgainstAMonster()
    {
        var (monster, hero) = GivenState(heroOwnerId: PlayerId);

        var preview = await Preview(monster.Id, hero.Id);

        Assert.True(preview.TargetPower > 0);
        Assert.True(preview.AttackerPower > 0);
        Assert.InRange(preview.WinChance, 0, 1);
    }
}
