using EmpireIdle.Application.Battles.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Battles;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Battles;

/// <summary>
/// Загін гравця для одномоментного бою: герої на своїх доріжках, стати з HeroStats, конвої з юнітів
/// гарнізону вдома, бонуси героя військам і — лише у вежі — бафи міста й звірів.
/// </summary>
public class BattleSquadFactoryTests
{
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly DateTime Now = Entities.Now;

    private readonly GameConfig _config;
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>().ForwardHeroLookups();
    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IActiveEffectRepository _effects = Substitute.For<IActiveEffectRepository>();
    private readonly Garrison _garrison;

    public BattleSquadFactoryTests()
    {
        _config = new GameConfigBuilder()
            .WithUnits()
            .WithHeroes(h =>
            {
                h.RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["knight"] = TestKeys.Cavalry, ["archer"] = TestKeys.Archer, ["mage"] = TestKeys.Archer };
                h.ConvoySize = 100;
                h.ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 2 }];
            })
            .Build();

        foreach (var hero in _config.Heroes)
            hero.BaseStats["Health"] = 500;

        _config.RealtimeBattle.HeroAttackIntervals = new() { ["warrior"] = 1.25, ["knight"] = 1.0, ["archer"] = 1.3, ["mage"] = 1.3 };
        _config.RealtimeBattle.UnitAttackIntervals = new() { [TestKeys.Infantry] = 1.25, [TestKeys.Cavalry] = 1.0, [TestKeys.Archer] = 1.4 };

        var village = Entities.Village(PlayerId);
        _garrison = new Garrison(Guid.NewGuid(), village.Id, serverId: 1);
        _villages.GetByPlayerIdReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(village);
        _garrisons.GetByVillageIdReadOnlyAsync(village.Id, Arg.Any<CancellationToken>()).Returns(_garrison);
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private BattleSquadFactory Factory()
    {
        var catalog = new GameCatalog(_config);
        var progression = new HeroProgression(_config.HeroSettings);

        return new BattleSquadFactory(_heroes, _inventory, _villages, _garrisons, catalog, new HeroStats(progression, catalog),
            new HeroSkills(_config.HeroSettings), new HeroCombatModifiers(catalog), TestEffects.Resolver(_effects, catalog: catalog));
    }

    private Hero Hero(string key = TestKeys.CommonHero, Guid? playerId = null)
    {
        var hero = Entities.Hero(key, playerId ?? PlayerId);
        _heroes.GetByIdAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(hero);
        return hero;
    }

    private void Home(int infantry) => _garrison.ReceiveUnits(new Dictionary<UnitStackKey, int> { [new(TestKeys.Infantry, 1)] = infantry }, Now);

    private void Boost(EffectTarget target, double multiplier)
        => _effects.GetAsync(PlayerId, target, Arg.Any<CancellationToken>())
            .Returns(new ActiveEffect(Guid.NewGuid(), PlayerId, target, multiplier, Now.AddHours(-1), Now.AddHours(1), "boost"));

    private Task<IReadOnlyList<SquadMember>> Build(IReadOnlyList<Guid?> lanes, bool withBuffs = false)
        => Factory().BuildAsync(PlayerId, lanes, withBuffs, Now, CancellationToken.None);

    /// <summary>Герой стоїть на своїй доріжці зі статами екрана героя; порожня доріжка — без героя.</summary>
    [Fact]
    public async Task BuildAsync_ShouldPutHeroesOnTheirLanes_WithTheirStats()
    {
        var bran = Hero();
        var iselle = Hero(TestKeys.UniqueHero);

        var squad = await Build([bran.Id, null, iselle.Id]);

        var catalog = new GameCatalog(_config);
        var stats = new HeroStats(new HeroProgression(_config.HeroSettings), catalog).Compute(bran, catalog.Hero(bran.HeroKey), []);

        Assert.Equal([0, 2], squad.Select(m => m.Lane));
        Assert.Equal(("warrior", TestKeys.Infantry), (squad[0].HeroClass, squad[0].UnitType));
        Assert.Equal(stats["Attack"], squad[0].Hero.Attack, 6);
        Assert.Equal(500 * _config.RealtimeBattle.HeroHealthScale, squad[0].Hero.MaxHealth, 6);
    }

    /// <summary>Конвої — з юнітів удома; герої тієї ж ролі набирають по черзі, тож другий не лишається без війська.</summary>
    [Fact]
    public async Task BuildAsync_ShouldLeadConvoysOfUnitsAtHome()
    {
        Home(infantry: 150);
        var first = Hero();
        var second = Hero(TestKeys.PlainHero);

        var squad = await Build([first.Id, second.Id]);

        Assert.Equal([100], squad[0].Convoys.Select(c => c.Units));
        Assert.Equal([50], squad[1].Convoys.Select(c => c.Units));
    }

    /// <summary>Пасивка героя на атаку піхоти підсилює його конвої, як і в марші; без героя бонус гасне.</summary>
    [Fact]
    public async Task BuildAsync_ShouldGiveConvoysTheHerosTroopBonus()
    {
        Home(infantry: 100);
        _config.Heroes.Single(h => h.Key == TestKeys.CommonHero).Skills.Add(Passives.Attack(20));

        var convoy = (await Build([Hero().Id]))[0].Convoys[0];

        Assert.Equal(convoy.Leaderless.Attack * 1.2, convoy.Led.Attack, 6);
    }

    /// <summary>У вежі бафи міста й звірів множать атаку й захист героїв і конвоїв; на арені — ні.</summary>
    [Fact]
    public async Task BuildAsync_ShouldApplyCityBuffs_OnlyWhenTheModeAllowsThem()
    {
        Home(infantry: 100);
        Boost(EffectTarget.Attack, 1.25);
        Boost(EffectTarget.Defense, 1.1);
        var bran = Hero();

        var arena = (await Build([bran.Id]))[0];
        var tower = (await Build([bran.Id], withBuffs: true))[0];

        Assert.Equal(arena.Hero.Attack * 1.25, tower.Hero.Attack, 6);
        Assert.Equal(arena.Hero.Defense * 1.1, tower.Hero.Defense, 6);
        Assert.Equal(arena.Hero.MaxHealth, tower.Hero.MaxHealth, 6);
        Assert.Equal(arena.Convoys[0].Led.Attack * 1.25, tower.Convoys[0].Led.Attack, 6);
        Assert.Equal(arena.Convoys[0].Leaderless.Defense * 1.1, tower.Convoys[0].Leaderless.Defense, 6);
    }

    /// <summary>Пораненого могли покласти між вибором складу й боєм — це чесна відмова з причиною.</summary>
    [Fact]
    public async Task BuildAsync_ShouldRefuseAWoundedHero()
    {
        var bran = Hero();
        bran.Wound(Now);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Build([bran.Id]));

        Assert.Equal(RefusalReasons.SquadHeroWounded.Key, refusal.Reason);
    }

    [Fact]
    public async Task BuildAsync_ShouldNotFindSomeoneElsesHero()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() => Build([Hero(playerId: Guid.NewGuid()).Id]));

    /// <summary>Порожній, подвійний чи задовгий склад шле лише кривий клієнт.</summary>
    [Fact]
    public async Task BuildAsync_ShouldRejectMalformedSquads()
    {
        var bran = Hero();

        await Assert.ThrowsAsync<ArgumentException>(() => Build([null]));
        await Assert.ThrowsAsync<ArgumentException>(() => Build([bran.Id, bran.Id]));
        await Assert.ThrowsAsync<ArgumentException>(() => Build([bran.Id, null, null, null, null, null]));
    }
}
