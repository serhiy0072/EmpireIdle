using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Кланове дерево відкривається шарами (рішення 12.11): передумови вузла — рівно весь попередній ряд вкладки,
/// без пропущених рядів, а тіри не мають гейтів ратуші й світу.
/// </summary>
public class ClanLayerValidationTests
{
    private static GameConfig With(Action<ClanTechnologiesConfig> tune)
        => new GameConfigBuilder().WithTechnologies(t => tune(t.Clan)).Build();

    private static TechnologyNodeConfig Node(string key, int row, params string[] requires) => new()
    {
        Key = key, Tab = "valor", Row = row, Levels = 1, Requires = [.. requires], Resource = TestKeys.Gold,
        Effects = [new TechnologyEffectConfig { Stat = TechnologyStat.Attack, PerLevel = 0.01 }]
    };

    [Fact]
    public void Validate_ShouldAccept_NodesThatRequireTheWholePreviousLayer()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(With(clan => clan.Nodes.Add(Node("extra", 3, "c_coins"))))));

    /// <summary>У другому ряду два вузли: вузол третього, що вимагає лише один із них, відкрився б раніше шару.</summary>
    [Fact]
    public void Validate_ShouldRefuse_ANodeThatRequiresOnlyPartOfThePreviousLayer()
    {
        var config = With(clan =>
        {
            clan.Nodes.Add(Node("sibling", 2, "v_attack_all"));
            clan.Nodes.Add(Node("next", 3, "c_coins"));
        });

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("'next'", error.Message);
    }

    [Fact]
    public void Validate_ShouldRefuse_AGapBetweenRows()
    {
        var config = With(clan => clan.Nodes.Add(Node("far", 4, "c_coins")));

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("'far'", error.Message);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(1, 5)]
    public void Validate_ShouldRefuse_AGatedClanTier(int worldLevel, int townHall)
    {
        var config = With(clan =>
        {
            clan.Tiers[1].WorldLevel = worldLevel;
            clan.Tiers[1].MainBuildingLevel = townHall;
        });

        var error = Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));

        Assert.Contains("must not be gated", error.Message);
    }
}
