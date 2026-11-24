using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Tests.Heroes;
using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Герої як бійці (варіант B): сила з атаки чи захисту, здоров'я — з повних статів героя.</summary>
public class HeroCombatantsTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly GameCatalog _catalog = HeroTestConfig.Catalog();
    private readonly HeroStats _stats;
    private readonly List<EquipmentItem> _equipped = [];

    public HeroCombatantsTests()
    {
        _stats = new HeroStats(new HeroProgression(_catalog.Config.HeroSettings), _catalog);

        // Спорядження всіх бійців читається одним запитом
        _inventory.GetEquippedByHeroesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => _equipped.Where(e => call.Arg<IReadOnlyCollection<Guid>>().Contains(e.EquippedByHeroId!.Value)).ToList());
    }

    // Дерево фікстури: attack_infantry додатково дає +25% захисту всім за рівень
    private HeroCombatants Combatants() => new(_inventory, _stats, _catalog,
        TestTechnologies.For(TestTechnologies.TreeWith(TechnologyStat.Defense, 0.25), _research));

    private void Equip(Hero hero, EquipmentItem item)
    {
        item.EquipTo(hero.Id, 0, Now);
        _equipped.Add(item);
    }

    private static Hero Hero(string key = HeroTestConfig.CommonHero)
        => new(Guid.NewGuid(), Guid.NewGuid(), 1, key, Guid.NewGuid(), Now);

    /// <summary>
    /// Атакує — сила з атаки з прихованим критом, обороняється — із захисту без криту; здоров'я — те саме.
    /// </summary>
    [Fact]
    public async Task ForAsync_ShouldTakeAttackWithCritWhenAttacking_AndDefenseWhenDefending()
    {
        var hero = Hero();
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), []);
        Assert.True(stats.GetValueOrDefault("Attack") > 0);

        var attacking = Assert.Single(await Combatants().ForAsync([hero], attacking: true, CancellationToken.None));
        var defending = Assert.Single(await Combatants().ForAsync([hero], attacking: false, CancellationToken.None));

        var crit = HeroCrit.DamageMultiplier(stats, _catalog.Config.Dungeons);
        Assert.True(crit > 1);
        Assert.Equal((hero.Id, stats.GetValueOrDefault("Health")), (attacking.HeroId, attacking.Health));
        Assert.Equal(stats.GetValueOrDefault("Attack") * crit, attacking.Power, 6);
        Assert.Equal(stats.GetValueOrDefault("Defense"), defending.Power);
    }

    /// <summary>
    /// Бойові технології власника множать силу героя в марші й обороні, як у вежі та данжі
    /// (рішення 03.11.2026); герой гравця без досліджень лишається при своїх статах.
    /// </summary>
    [Fact]
    public async Task ForAsync_ShouldStrengthenTheHero_WithTheOwnersTechnologies()
    {
        var hero = Hero();
        var stranger = Hero();
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), []);
        _research.Researched(hero.PlayerId, "attack_infantry", 1);

        var defending = await Combatants().ForAsync([hero, stranger], attacking: false, CancellationToken.None);

        Assert.Equal(stats.GetValueOrDefault("Defense") * 1.25, defending.Single(c => c.HeroId == hero.Id).Power, 6);
        Assert.Equal(stats.GetValueOrDefault("Defense"), defending.Single(c => c.HeroId == stranger.Id).Power, 6);
    }

    /// <summary>Крит зі спорядження підсилює атаку героя в марші понад приховану базу.</summary>
    [Fact]
    public async Task ForAsync_ShouldStrengthenTheAttack_WithCritFromTheGear()
    {
        var hero = Hero();
        var necklace = EmpireIdle.TestKit.Entities.Equipment(HeroTestConfig.Artifact, Domain.Enums.EquipmentSlot.Artifact, bonuses: (0, "CritChance", 20));
        Equip(hero, necklace);
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), [necklace]);

        var attacking = Assert.Single(await Combatants().ForAsync([hero], attacking: true, CancellationToken.None));

        // База 10% + 20% зі спорядження, множник криту 1.6: 1 + 0.3 × 0.6
        Assert.Equal(stats["Attack"] * 1.18, attacking.Power, 6);
    }

    /// <summary>
    /// Сила військам зі спорядження — UnitAttack у нападі, UnitDefense в обороні, з вагою UnitStatPower;
    /// оберіг з відсотків стає часткою.
    /// </summary>
    [Fact]
    public async Task ForAsync_ShouldTakeTroopPowerAndWard_FromTheGear()
    {
        var hero = Hero();
        var necklace = EmpireIdle.TestKit.Entities.Equipment(HeroTestConfig.Artifact, Domain.Enums.EquipmentSlot.Artifact, bonuses: (0, "Ward", 16));
        Equip(hero, necklace);
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), [necklace]);
        Assert.True(stats.GetValueOrDefault(ArtifactStats.UnitAttack) > 0);

        var attacking = Assert.Single(await Combatants().ForAsync([hero], attacking: true, CancellationToken.None));
        var defending = Assert.Single(await Combatants().ForAsync([hero], attacking: false, CancellationToken.None));

        var weight = _catalog.Config.Equipment.UnitStatPower;
        Assert.Equal(stats[ArtifactStats.UnitAttack] * weight, attacking.TroopPower, 6);
        Assert.Equal(stats[ArtifactStats.UnitDefense] * weight, defending.TroopPower, 6);
        Assert.Equal(0.16, attacking.Ward, 6);
    }

    /// <summary>Бонус військам зі спорядження — з підсумкових статів героя; поранений нічого не дає.</summary>
    [Fact]
    public async Task GearAsync_ShouldReadTroopBonuses_FromTheHerosGear()
    {
        var hero = Hero();
        var wounded = Hero();
        wounded.Wound(Now);
        var belt = EmpireIdle.TestKit.Entities.Equipment(HeroTestConfig.Artifact, Domain.Enums.EquipmentSlot.Artifact, bonuses: (0, "TroopsAttack", 8));
        var woundedBelt = EmpireIdle.TestKit.Entities.Equipment(HeroTestConfig.Artifact, Domain.Enums.EquipmentSlot.Artifact, bonuses: (0, "TroopsAttack", 8));
        Equip(hero, belt);
        Equip(wounded, woundedBelt);

        var gear = await Combatants().GearAsync([hero, wounded, null], CancellationToken.None);

        Assert.Equal(8, gear.Attack(hero.Id), 6);
        Assert.Equal(0, gear.Defense(hero.Id), 6);
        Assert.Equal(0, gear.Attack(wounded.Id), 6);
    }

    /// <summary>Поранений не б'ється, порожнього місця й невідомого героя теж немає.</summary>
    [Fact]
    public async Task ForAsync_ShouldSkipWoundedMissingAndUnknownHeroes()
    {
        var wounded = Hero();
        wounded.Wound(Now);

        var combatants = await Combatants().ForAsync([wounded, null, Hero("retired_hero")], attacking: true, CancellationToken.None);

        Assert.Empty(combatants);
    }
}
