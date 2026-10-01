using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Форма кривої важливіша за конкретні числа: довге прискорення відчутне, але в межах
/// одного пакета gems. Останню хвилину прискорення не зрізає, і безкоштовного фінішу немає.
/// </summary>
public class SpeedUpCalculatorTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SpeedUpCalculator _calculator = new(new MonetizationConfig
    {
        SpeedUpFactor = 1.2,
        SpeedUpExponent = 0.75,
        SpeedUpFloorSeconds = 60
    });

    /// <summary>
    /// Сублінійна крива за зрізану частину (залишок мінус хвилина межі):
    /// подвоєння часу дає приблизно +68% ціни.
    /// </summary>
    [Theory]
    [InlineData(31, 16)]      // 1.2 × 30^0.75   = 15.4  → 16
    [InlineData(121, 44)]     // 1.2 × 120^0.75  = 43.3  → 44
    [InlineData(601, 146)]    // 1.2 × 600^0.75  = 145.5 → 146
    [InlineData(4321, 640)]   // 1.2 × 4320^0.75 = 639.6 → 640
    public void GetCost_ShouldFollowTheSublinearCurve_OverTheCutPart(int minutes, int expected)
    {
        var cost = _calculator.GetCost(Now.AddMinutes(minutes), Now);

        Assert.Equal(expected, cost);
    }

    /// <summary>Безкоштовного фінішу немає: кілька секунд понад межу — це вже щонайменше 1 gem.</summary>
    [Fact]
    public void GetCost_ShouldBeAtLeastOneGem_JustAboveTheFloor()
    {
        Assert.Equal(1, _calculator.GetCost(Now.AddSeconds(61), Now));
    }

    /// <summary>Колишній безкоштовний поріг (3 хв) тепер платний.</summary>
    [Fact]
    public void GetCost_ShouldCharge_ForAThreeMinuteTimer()
    {
        Assert.True(_calculator.GetCost(Now.AddMinutes(3), Now) > 0);
    }

    /// <summary>На межі й нижче прискорювати нічого — ціни немає.</summary>
    [Theory]
    [InlineData(60)]
    [InlineData(30)]
    [InlineData(0)]
    public void GetCost_ShouldBeZero_AtOrBelowTheFloor(int secondsLeft)
    {
        Assert.Equal(0, _calculator.GetCost(Now.AddSeconds(secondsLeft), Now));
    }

    /// <summary>Зрізається все понад межу: після прискорення лишається рівно хвилина.</summary>
    [Fact]
    public void GetCut_ShouldLeaveExactlyTheFloor()
    {
        var completesAt = Now.AddHours(2);

        var cut = _calculator.GetCut(completesAt, Now);

        Assert.Equal(Now.AddSeconds(60), completesAt - cut);
    }

    /// <summary>Прострочений таймер не зсувається вперед: зрізати нічого.</summary>
    [Fact]
    public void GetCut_ShouldBeZero_ForAnOverdueTimer()
    {
        Assert.Equal(TimeSpan.Zero, _calculator.GetCut(Now.AddMinutes(-5), Now));
    }

    /// <summary>Команді прискорення на межі — відмова з причиною й межею в параметрі.</summary>
    [Fact]
    public void RequireCut_ShouldRefuse_AtTheFloor()
    {
        var refusal = Assert.Throws<InvalidStateException>(() => _calculator.RequireCut(Now.AddSeconds(45), Now));

        Assert.Equal(RefusalReasons.SpeedUpAtFloor.Key, refusal.Reason);
    }
}
