using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Особиста книга: спроби відновлюються поступово, денні лічильники скидаються о 00:00 UTC.</summary>
public class ClanOfferingLedgerTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Recharge = TimeSpan.FromSeconds(576);

    private static ClanOfferingLedger Ledger() => new(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

    [Fact]
    public void NewLedger_ShouldHaveTheWholeScale()
        => Assert.Equal(25, Ledger().AttemptsAvailable(25, Recharge, Now));

    [Fact]
    public void SpendAttempt_ShouldRefuse_WhenTheScaleIsEmpty()
    {
        var ledger = Ledger();

        for (var i = 0; i < 25; i++)
            ledger.SpendAttempt(25, Recharge, Now);

        var error = Assert.Throws<RequirementNotMetException>(() => ledger.SpendAttempt(25, Recharge, Now));

        Assert.Equal(RefusalReasons.ClanTechNoAttempts.Key, error.Reason);
    }

    [Fact]
    public void Attempts_ShouldRecoverOneByOne_AndTheWholeScaleInFourHours()
    {
        var ledger = Ledger();

        for (var i = 0; i < 25; i++)
            ledger.SpendAttempt(25, Recharge, Now);

        Assert.Equal(0, ledger.AttemptsAvailable(25, Recharge, Now + Recharge - TimeSpan.FromSeconds(1)));
        Assert.Equal(1, ledger.AttemptsAvailable(25, Recharge, Now + Recharge));
        Assert.Equal(25, ledger.AttemptsAvailable(25, Recharge, Now + TimeSpan.FromHours(4)));
        Assert.Equal(25, ledger.AttemptsAvailable(25, Recharge, Now + TimeSpan.FromDays(3)));
    }

    /// <summary>Спроба посеред інтервалу не скидає вже набіжаного часу: інакше часті витрати зупиняли б відновлення.</summary>
    [Fact]
    public void SpendAttempt_ShouldKeepThePartOfTheIntervalThatPassed()
    {
        var ledger = Ledger();
        ledger.SpendAttempt(25, Recharge, Now);
        ledger.SpendAttempt(25, Recharge, Now);

        // Минуло півтора інтервали: одна спроба повернулась, половина наступної вже набігла
        var later = Now + Recharge * 1.5;
        ledger.SpendAttempt(25, Recharge, later);

        Assert.Equal(23, ledger.AttemptsAvailable(25, Recharge, later));
        Assert.Equal(24, ledger.AttemptsAvailable(25, Recharge, Now + Recharge * 2));
    }

    /// <summary>Клан із вищою стелею дає більше, вихід забирає надлишок, але витрачене не дарується.</summary>
    [Fact]
    public void Attempts_ShouldFollowTheCeiling_WithoutGivingAnythingBack()
    {
        var ledger = Ledger();

        for (var i = 0; i < 25; i++)
            ledger.SpendAttempt(25, Recharge, Now);

        Assert.Equal(3, ledger.AttemptsAvailable(28, Recharge, Now));
        Assert.Equal(0, ledger.AttemptsAvailable(25, Recharge, Now));
    }

    [Fact]
    public void GemCounter_ShouldResetAtUtcMidnight()
    {
        var ledger = Ledger();
        ledger.RecordGemOffering(Now);
        ledger.RecordGemOffering(Now);

        Assert.Equal(3, ledger.NextGemOffering(Now));
        Assert.Equal(1, ledger.NextGemOffering(Now.Date.AddDays(1)));
    }

    [Fact]
    public void AddPoints_ShouldReportTheDayTotal_AndStartAnewTheNextDay()
    {
        var ledger = Ledger();

        Assert.Equal((0, 10), ledger.AddPoints(10, Now));
        Assert.Equal((10, 60), ledger.AddPoints(50, Now));
        Assert.Equal((0, 10), ledger.AddPoints(10, Now.Date.AddDays(1)));
    }
}
