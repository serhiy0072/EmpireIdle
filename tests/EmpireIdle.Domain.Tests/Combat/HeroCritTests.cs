using EmpireIdle.Domain.Combat;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Combat;

/// <summary>Крит героя в марші — очікувана шкода: 1 + шанс × (множник − 1).</summary>
public class HeroCritTests
{
    private static readonly DungeonsConfig Config = new() { BaseCritChance = 0.1, CritMultiplier = 1.6, MaxCritChance = 0.6 };

    /// <summary>Без бонусів діє прихована база: 1 + 0.1 × 0.6.</summary>
    [Fact]
    public void DamageMultiplier_ShouldApplyTheHiddenBase_WithoutGear()
        => Assert.Equal(1.06, HeroCrit.DamageMultiplier(new Dictionary<string, double>(), Config), 6);

    /// <summary>Шанс і сила криту з відсотків героя: 1 + (0.1 + 0.2) × (1.6 + 0.4 − 1).</summary>
    [Fact]
    public void DamageMultiplier_ShouldAddTheHerosCritChanceAndPower()
    {
        var stats = new Dictionary<string, double> { [HeroCrit.CritChance] = 20, [HeroCrit.CritPower] = 40 };

        Assert.Equal(1.3, HeroCrit.DamageMultiplier(stats, Config), 6);
    }

    /// <summary>Шанс упирається в ту саму стелю, що й у данжі: 1 + 0.6 × 0.6.</summary>
    [Fact]
    public void DamageMultiplier_ShouldCapTheChance()
    {
        var stats = new Dictionary<string, double> { [HeroCrit.CritChance] = 90 };

        Assert.Equal(1.36, HeroCrit.DamageMultiplier(stats, Config), 6);
    }
}
