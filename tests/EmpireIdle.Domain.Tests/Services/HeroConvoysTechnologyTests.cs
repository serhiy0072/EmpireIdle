using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Технології збільшують кожен конвой героя, а не кількість конвоїв (рішення 28.10.2026):
/// герой першого рівня веде 2 конвої по 50 піхоти, +4 у конвой — уже 2 × 54.
/// </summary>
public class HeroConvoysTechnologyTests
{
    private static readonly HeroesConfig Config = new()
    {
        RoleUnits = new() { ["warrior"] = TestKeys.Infantry, ["archer"] = TestKeys.Archer },
        ConvoySize = 50,
        ConvoysByLevel = [new ConvoyStepConfig { Level = 1, Convoys = 2 }]
    };

    private static readonly HeroConvoys Convoys = new(Config);

    private static readonly HeroConfig Warrior = new() { Key = TestKeys.CommonHero, Class = "warrior" };

    private static TechnologyBonuses ConvoySize(string? target, double value)
    {
        var bonuses = new TechnologyBonuses();
        bonuses.Add(TechnologyStat.ConvoySize, target, value);
        return bonuses;
    }

    private static Dictionary<UnitStackKey, int> Infantry(int count) => new() { [new(TestKeys.Infantry, 1)] = count };

    [Fact]
    public void Capacity_ShouldGrowEveryConvoy()
        => Assert.Equal(108, Convoys.Capacity(TestKit.Entities.Hero(), extraPerConvoy: 4));

    /// <summary>Бонус «для всіх» і бонус типу складаються; бонус чужого типу не діє.</summary>
    [Fact]
    public void ExtraConvoySize_ShouldAddTheAllUnitsBonusAndTheTypeBonus()
    {
        var bonuses = ConvoySize(null, 5);
        bonuses.Add(TechnologyStat.ConvoySize, TestKeys.Infantry, 4);

        Assert.Equal(9, HeroConvoys.ExtraConvoySize(bonuses, TestKeys.Infantry));
        Assert.Equal(5, HeroConvoys.ExtraConvoySize(bonuses, TestKeys.Archer));
        Assert.Equal(0, HeroConvoys.ExtraConvoySize(null, TestKeys.Infantry));
    }

    [Fact]
    public void EnsureCanLead_ShouldLetTheBiggerConvoysThrough()
    {
        var leaders = new[] { (TestKit.Entities.Hero(), (HeroConfig?)Warrior) };

        Assert.Throws<RequirementNotMetException>(() => Convoys.EnsureCanLead(leaders, Infantry(108)));

        Convoys.EnsureCanLead(leaders, Infantry(108), ConvoySize(TestKeys.Infantry, 4));
    }

    [Fact]
    public void EnsureCanLead_ShouldStillRefuse_AboveTheGrownCapacity()
    {
        var leaders = new[] { (TestKit.Entities.Hero(), (HeroConfig?)Warrior) };

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            Convoys.EnsureCanLead(leaders, Infantry(109), ConvoySize(TestKeys.Infantry, 4)));

        Assert.Equal(108, refusal.Args["capacity"]);
    }
}
