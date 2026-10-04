using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Звірі в конфігу (GDD §5.10). Кожен тест ламає одну річ у валідному конфігу:
/// звір із наявного монстра, звіринець із місцями, корм-предмет.
/// </summary>
public class BeastConfigValidationTests
{
    private static GameConfig Valid() => new GameConfigBuilder().WithBeasts().Build();

    private static InvalidOperationException Rejects(Action<GameConfig> break_)
    {
        var config = Valid();
        break_(config);

        return Assert.Throws<InvalidOperationException>(() => GameConfigValidator.Validate(config));
    }

    [Fact]
    public void Validate_ShouldAcceptTheBeastConfig()
        => Assert.Null(Record.Exception(() => GameConfigValidator.Validate(Valid())));

    [Fact]
    public void Validate_ShouldRejectABeastOfAnUnknownMonster()
        => Assert.Contains(TestKeys.Beast, Rejects(c => c.Beasts.Types[0].MonsterKey = "ghosts").Message);

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Validate_ShouldRejectATameChanceOutsideTheRange(double chance)
        => Rejects(c => c.Beasts.Types[0].TameChance = chance);

    /// <summary>Два звірі з одного монстра: тип монстра однозначно визначає звіра.</summary>
    [Fact]
    public void Validate_ShouldRejectTwoBeastsOfOneMonster()
        => Rejects(c => c.Beasts.Types.Add(
            new BeastConfig { Key = "dire_wolf", MonsterKey = TestKeys.BeastMonster, TameChance = 0.1 }));

    [Fact]
    public void Validate_ShouldRejectAChanceCapBelowTheBase()
        => Rejects(c => c.Beasts.MaxTameChanceMultiplier = 0.5);

    [Fact]
    public void Validate_ShouldRejectBeastsWithoutAPen()
        => Assert.Contains("beast slots",
            Rejects(c => c.Buildings.Single(b => b.Key == TestKeys.BeastPen).BeastCapacityPerLevel = 0).Message);

    /// <summary>Годувати нічим: корм мусить бути стаковим предметом із каталогу.</summary>
    [Fact]
    public void Validate_ShouldRejectAnUnknownFeedItem()
        => Assert.Contains("FeedItemKey", Rejects(c => c.Beasts.FeedItemKey = "nothing").Message);

    [Theory]
    [InlineData(0, 100, 1.25)]
    [InlineData(10, 0, 1.25)]
    [InlineData(10, 100, 0.9)]
    public void Validate_ShouldRejectBrokenLeveling(int levelsPerRank, int baseExperience, double growth)
        => Rejects(c =>
        {
            c.Beasts.LevelsPerRank = levelsPerRank;
            c.Beasts.BaseExperience = baseExperience;
            c.Beasts.ExperienceGrowth = growth;
        });
}
