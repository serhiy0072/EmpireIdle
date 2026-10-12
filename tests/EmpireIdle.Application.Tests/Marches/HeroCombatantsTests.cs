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
        => new(Guid.NewGuid(), Guid.NewGuid(), 1, key, Guid.NewGuid(), asLeader: true, Now);

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

    /// <summary>Поранений не б'ється, порожнього місця лідера й невідомого героя теж немає.</summary>
    [Fact]
    public async Task ForAsync_ShouldSkipWoundedMissingAndUnknownHeroes()
    {
        var wounded = Hero();
        wounded.Wound(Now);

        var combatants = await Combatants().ForAsync([wounded, null, Hero("retired_hero")], attacking: true, CancellationToken.None);

        Assert.Empty(combatants);
    }
}
