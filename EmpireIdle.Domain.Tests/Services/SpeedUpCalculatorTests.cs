using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Форма кривої важливіша за конкретні числа: коротке прискорення має бути
/// майже безкоштовним, довге — відчутним, але в межах одного пакета gems.
/// </summary>
public class SpeedUpCalculatorTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SpeedUpCalculator _calculator = new(new MonetizationConfig
    {
        SpeedUpFactor = 1.2,
        SpeedUpExponent = 0.75,
        SpeedUpMinGems = 1,
        InstantFinishThresholdMinutes = 5
    });

    /// <summary>
    /// Сублінійна крива: подвоєння часу дає приблизно +68% ціни.
    /// Триденний таймер має лишатись у межах середнього пакета.
    /// </summary>
    [Theory]
    [InlineData(30, 16)]      // 1.2 × 30^0.75   = 15.4  → 16
    [InlineData(120, 44)]     // 1.2 × 120^0.75  = 43.3  → 44
    [InlineData(600, 146)]    // 1.2 × 600^0.75  = 145.5 → 146
    [InlineData(4320, 640)]   // 1.2 × 4320^0.75 = 639.6 → 640
    public void GetInstantFinishCost_ShouldFollowTheSublinearCurve(int minutes, int expected)
    {
        var cost = _calculator.GetInstantFinishCost(Now.AddMinutes(minutes), Now);

        Assert.Equal(expected, cost);
    }

    /// <summary>Дрібні таймери безкоштовні — це зручність, а не покупка.</summary>
    [Fact]
    public void GetInstantFinishCost_ShouldBeFree_BelowThreshold()
    {
        Assert.Equal(0, _calculator.GetInstantFinishCost(Now.AddMinutes(4), Now));
    }

    /// <summary>Завершений таймер нічого не коштує.</summary>
    [Fact]
    public void GetInstantFinishCost_ShouldBeFree_WhenNothingRemains()
    {
        Assert.Equal(0, _calculator.GetInstantFinishCost(Now, Now));
    }
}
