using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Шанс приручення: база + бонус за рівень звіринця, зі стелею (GDD §5.10).</summary>
public class BeastTamingTests
{
    private static readonly BeastConfig Wolf = new() { Key = "wolf", MonsterKey = "wolves", TameChance = 0.2 };

    private static BeastTaming Taming(double perPenLevel = 0.01, double capMultiplier = 2.0)
    {
        var config = new GameConfigBuilder().WithBuildings("beastpen").Build();

        config.Buildings.Single(b => b.Key == "beastpen").BeastCapacityPerLevel = 1;
        config.Monsters = [new MonsterConfig { Key = "wolves", DisplayName = "Вовки", Units = [], Rewards = [] }];
        config.Beasts = new BeastsConfig
        {
            TameChancePerPenLevel = perPenLevel,
            MaxTameChanceMultiplier = capMultiplier,
            PityWins = 10,
            MaxRank = 5,
            Types = [Wolf]
        };

        return new BeastTaming(new GameCatalog(config));
    }

    [Fact]
    public void ChanceFor_ShouldAddTheBonusPerPenLevel()
        => Assert.Equal(0.25, Taming().ChanceFor(Wolf, penLevel: 5), precision: 10);

    /// <summary>Високий звіринець не робить приручення гарантованим: стеля — подвоєна база.</summary>
    [Fact]
    public void ChanceFor_ShouldStopAtTheCap()
        => Assert.Equal(0.4, Taming().ChanceFor(Wolf, penLevel: 30), precision: 10);

    [Fact]
    public void ChanceFor_ShouldNeverExceedCertainty()
        => Assert.Equal(1.0, Taming(perPenLevel: 0.5, capMultiplier: 10).ChanceFor(Wolf, penLevel: 30), precision: 10);

    [Fact]
    public void ForMonster_ShouldFindTheBeast_OnlyForItsMonster()
    {
        var taming = Taming();

        Assert.Equal("wolf", taming.ForMonster("wolves")?.Key);
        Assert.Null(taming.ForMonster("bats"));
    }
}
