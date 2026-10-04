using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>
/// Звірі в конфігу (GDD §5.10). Кожен тест ламає одну річ у валідному конфігу:
/// звір із наявного монстра, звіринець із місцями.
/// </summary>
public class BeastConfigValidationTests
{
    private const string Pen = "beastpen";

    private static GameConfig Valid()
    {
        var config = new GameConfigBuilder().WithBuildings(Pen).Build();

        config.Buildings.Single(b => b.Key == Pen).BeastCapacityPerLevel = 1;
        config.Monsters = [new MonsterConfig { Key = "wolves", DisplayName = "Вовки", Units = [], Rewards = [] }];
        config.Beasts = new BeastsConfig
        {
            TameChancePerPenLevel = 0.01,
            MaxTameChanceMultiplier = 2,
            PityWins = 10,
            MaxRank = 5,
            Types = [new BeastConfig { Key = "wolf", DisplayName = "Вовк", MonsterKey = "wolves", TameChance = 0.2 }]
        };

        return config;
    }

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
        => Assert.Contains("wolf", Rejects(c => c.Beasts.Types[0].MonsterKey = "ghosts").Message);

    [Theory]
    [InlineData(0)]
    [InlineData(1.5)]
    public void Validate_ShouldRejectATameChanceOutsideTheRange(double chance)
        => Rejects(c => c.Beasts.Types[0].TameChance = chance);

    /// <summary>Два звірі з одного монстра: тип монстра однозначно визначає звіра.</summary>
    [Fact]
    public void Validate_ShouldRejectTwoBeastsOfOneMonster()
        => Rejects(c => c.Beasts.Types.Add(new BeastConfig { Key = "dire_wolf", MonsterKey = "wolves", TameChance = 0.1 }));

    [Fact]
    public void Validate_ShouldRejectAChanceCapBelowTheBase()
        => Rejects(c => c.Beasts.MaxTameChanceMultiplier = 0.5);

    [Fact]
    public void Validate_ShouldRejectBeastsWithoutAPen()
        => Assert.Contains("beast slots", Rejects(c => c.Buildings.Single(b => b.Key == Pen).BeastCapacityPerLevel = 0).Message);
}
