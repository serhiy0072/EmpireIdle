using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Application.Tests.Heroes;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Герої як бійці (варіант B): сила з атаки чи захисту, здоров'я — з повних статів героя.</summary>
public class HeroCombatantsTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private readonly IInventoryRepository _inventory = Substitute.For<IInventoryRepository>();
    private readonly GameCatalog _catalog = HeroTestConfig.Catalog();
    private readonly HeroStats _stats;

    public HeroCombatantsTests()
    {
        _stats = new HeroStats(new HeroProgression(_catalog.Config.HeroSettings), _catalog);
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem>());
    }

    private HeroCombatants Combatants() => new(_inventory, _stats, _catalog);

    private static Hero Hero(string key = HeroTestConfig.CommonHero)
        => new(Guid.NewGuid(), Guid.NewGuid(), 1, key, Guid.NewGuid(), Now);

    /// <summary>Атакує — сила з атаки, обороняється — із захисту; здоров'я — те саме.</summary>
    [Fact]
    public async Task ForAsync_ShouldTakeAttackWhenAttacking_AndDefenseWhenDefending()
    {
        var hero = Hero();
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), []);
        Assert.True(stats.GetValueOrDefault("Attack") > 0);

        var attacking = Assert.Single(await Combatants().ForAsync([hero], attacking: true, CancellationToken.None));
        var defending = Assert.Single(await Combatants().ForAsync([hero], attacking: false, CancellationToken.None));

        Assert.Equal((hero.Id, stats.GetValueOrDefault("Attack"), stats.GetValueOrDefault("Health")), (attacking.HeroId, attacking.Power, attacking.Health));
        Assert.Equal(stats.GetValueOrDefault("Defense"), defending.Power);
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
        _inventory.GetEquippedAsync(hero.Id, Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem> { necklace });
        var stats = _stats.Compute(hero, _catalog.Hero(hero.HeroKey), [necklace]);
        Assert.True(stats.GetValueOrDefault(ArtifactStats.UnitAttack) > 0);

        var attacking = Assert.Single(await Combatants().ForAsync([hero], attacking: true, CancellationToken.None));
        var defending = Assert.Single(await Combatants().ForAsync([hero], attacking: false, CancellationToken.None));

        var weight = _catalog.Config.Equipment.UnitStatPower;
        Assert.Equal(stats[ArtifactStats.UnitAttack] * weight, attacking.TroopPower, 6);
        Assert.Equal(stats[ArtifactStats.UnitDefense] * weight, defending.TroopPower, 6);
        Assert.Equal(0.16, attacking.Ward, 6);
    }

    /// <summary>Бонуси зі спорядження — з підсумкових статів героя; порожнє місце нічого не дає.</summary>
    [Fact]
    public async Task GearAsync_ShouldReadTroopBonuses_FromTheHerosGear()
    {
        var hero = Hero();
        var belt = EmpireIdle.TestKit.Entities.Equipment(HeroTestConfig.Artifact, Domain.Enums.EquipmentSlot.Artifact, bonuses: (0, "TroopsAttack", 8));
        _inventory.GetEquippedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<EquipmentItem> { belt });

        var gear = await Combatants().GearAsync([hero, null], CancellationToken.None);

        Assert.Equal(8, gear.Attack(hero.Id), 6);
        Assert.Equal(0, gear.Defense(hero.Id), 6);
        Assert.Equal(0, gear.Attack(Guid.NewGuid()), 6);
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
