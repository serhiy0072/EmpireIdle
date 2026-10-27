using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Строк зберігання не коротший за того, хто на цю історію спирається.</summary>
public class RetentionValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithHeroes().WithEquipment().WithDungeons().Build();

    [Fact]
    public void DefaultRetention_ShouldPass() => GameConfigValidator.Validate(Valid());

    [Fact]
    public void ListingsKeptShorterThanTheMedianWindow_ShouldFail()
    {
        var config = Valid();
        config.Market.MedianWindowHours = 72;
        config.Retention.ClosedMarketListingDays = 2;

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        Assert.Contains("ClosedMarketListingDays", error.Message);
    }

    [Fact]
    public void ScoutReportsKeptShorterThanTheirLetters_ShouldFail()
    {
        var config = Valid();
        config.Retention.ScoutReportDays = config.Mail.LetterRetentionDays - 1;

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        Assert.Contains("ScoutReportDays", error.Message);
    }

    [Fact]
    public void RequestsKeptShorterThanTheRejectionCooldown_ShouldFail()
    {
        var config = Valid();
        config.Clan.RejectedCooldownHours = 48;
        config.Retention.ResolvedClanRequestDays = 1;

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
        Assert.Contains("ResolvedClanRequestDays", error.Message);
    }
}
