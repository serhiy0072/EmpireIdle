using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Шкала енергії данжів. Наповнюється розрахунком від часу, тож усі межові
/// випадки — це арифметика, яку варто закріпити тестом раз і назавжди.
/// </summary>
public class DungeonEnergyTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private const int Max = 100;
    private const double RegenHours = 20;

    private static DungeonEnergy Energy(int amount, DateTime? at = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), amount, at ?? Now);

    /// <summary>Повна шкала за двадцять годин означає п'ять одиниць на годину.</summary>
    [Fact]
    public void Current_ShouldRefillLinearly()
    {
        var energy = Energy(0);

        Assert.Equal(0, energy.Current(Max, RegenHours, Now));
        Assert.Equal(5, energy.Current(Max, RegenHours, Now.AddHours(1)));
        Assert.Equal(50, energy.Current(Max, RegenHours, Now.AddHours(10)));
    }

    [Fact]
    public void Current_ShouldStopAtTheCap()
    {
        var energy = Energy(0);

        Assert.Equal(Max, energy.Current(Max, RegenHours, Now.AddHours(RegenHours)));
        Assert.Equal(Max, energy.Current(Max, RegenHours, Now.AddDays(7)));
    }

    /// <summary>Годинник клієнта може відставати — назад шкала не котиться.</summary>
    [Fact]
    public void Current_ShouldNotDrop_WhenTimeGoesBackwards()
        => Assert.Equal(40, Energy(40).Current(Max, RegenHours, Now.AddHours(-3)));

    [Fact]
    public void Spend_ShouldTakeTheCost_FromTheAccruedValue()
    {
        var energy = Energy(0);

        // За дві години набігло десять — саме стільки коштує забіг
        energy.Spend(10, Max, RegenHours, Now.AddHours(2));

        Assert.Equal(0, energy.Current(Max, RegenHours, Now.AddHours(2)));
    }

    [Fact]
    public void Spend_ShouldReject_WhenTheGaugeIsShort()
    {
        var energy = Energy(5);

        var error = Assert.Throws<NotEnoughResourcesException>(() => energy.Spend(10, Max, RegenHours, Now));

        Assert.Equal("dungeon-energy", error.Resource);
        Assert.Equal(10, error.Need);
        Assert.Equal(5, error.Have);
    }

    /// <summary>Повна шкала за двадцять годин — це десять забігів по десять одиниць.</summary>
    [Fact]
    public void Spend_ShouldAllowTenRuns_OnAFullGauge()
    {
        var energy = Energy(Max);

        for (var run = 0; run < 10; run++)
            energy.Spend(10, Max, RegenHours, Now);

        Assert.Throws<NotEnoughResourcesException>(() => energy.Spend(10, Max, RegenHours, Now));
    }

    [Fact]
    public void FullAt_ShouldPointAtTheMomentTheGaugeFills()
    {
        var energy = Energy(50);

        Assert.Equal(Now.AddHours(10), energy.FullAt(Max, RegenHours, Now));
        Assert.Null(Energy(Max).FullAt(Max, RegenHours, Now));
    }

    [Fact]
    public void Add_ShouldStackOnTopOfTheAccruedValue_WithoutExceedingTheCap()
    {
        var energy = Energy(0);

        energy.Add(20, Max, RegenHours, Now.AddHours(2));

        Assert.Equal(30, energy.Current(Max, RegenHours, Now.AddHours(2)));

        energy.Add(500, Max, RegenHours, Now.AddHours(2));

        Assert.Equal(Max, energy.Current(Max, RegenHours, Now.AddHours(2)));
    }
}
