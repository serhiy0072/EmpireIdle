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
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();

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
            _monsters, _villages, _garrisons, _heroes, new MonsterArmyBuilder(catalog), heroModifiers, catalog,
            new VillageStatus(catalog));

        return new GetBattlePreviewQueryHandler(
            _villages, _garrisons, _heroes, _serverContext,
            new CombatCalculator(config.Combat, catalog), terrain, new MarchCalculator(terrain, catalog),
            new EffectResolver(_effects), new FakeTimeProvider(Now), targets, heroModifiers, catalog,
            new HeroProgression(config.HeroSettings));
    }

    /// <summary>Село з піхотою, монстр поруч; повертає монстра й героя з заданим власником.</summary>
    private (Monster Monster, Hero Hero) GivenState(Guid heroOwnerId)
    {
        var village = new Village(Guid.NewGuid(), PlayerId, "Test", ["food"], 50, 50);
        var garrison = new Garrison(Guid.NewGuid(), village.Id, 1);
        garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new UnitStackKey("infantry", 1)] = 10 }, Now);

        var monster = new Monster(Guid.NewGuid(), 1, "wolves", 1, 55, 55, Now);
        var hero = new Hero(Guid.NewGuid(), heroOwnerId, 1, "warrior_bran", Guid.NewGuid(), asLeader: true, Now);

        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(garrison);
        _monsters.GetByIdAsync(monster.Id, Arg.Any<CancellationToken>()).Returns(monster);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);

        return (monster, hero);
    }

    private Task<BattlePreviewResult> Preview(Guid monsterId, Guid heroId) => Handler().Handle(
        new GetBattlePreviewQuery(PlayerId, MarchTargetType.Monster, monsterId, heroId,
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
}
