using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Кожен клановий вузол мусить мати ціну спроби: ресурс із курсом і додатний множник тіру.</summary>
public class ClanOfferingPriceValidationTests
{
    private static GameConfig With(Action<ClanTechnologiesConfig> tune)
        => new GameConfigBuilder().WithTechnologies(t => tune(t.Clan)).Build();

    [Fact]
    public void Validate_ShouldAccept_NodesWithPricedResources()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(With(_ => { }))));

    [Fact]
    public void Validate_ShouldRefuse_ANodeWithoutAResource()
    {
        var config = With(clan => clan.Nodes[0].Resource = null);

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("v_attack_all", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_ANodeWhoseResourceHasNoRate()
    {
        var config = With(clan => clan.Offering.ResourceRates.Remove(TestKeys.Wood));

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("c_coins", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_ARateForAnUnknownResource()
    {
        var config = With(clan => clan.Offering.ResourceRates["mithril"] = 1);

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("mithril", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_AZeroRate()
    {
        var config = With(clan => clan.Offering.ResourceRates[TestKeys.Gold] = 0);

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("ResourceRates", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_ANonPositiveTierCostMultiplier()
    {
        var config = With(clan => clan.Tiers[1].AttemptCostMultiplier = 0);

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("AttemptCostMultiplier", error.Message);
    }
}
