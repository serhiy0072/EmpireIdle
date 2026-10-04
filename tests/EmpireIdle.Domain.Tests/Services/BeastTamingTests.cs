using EmpireIdle.Domain.Services;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>Шанс приручення й рівні звірів (GDD §5.10).</summary>
public class BeastTamingTests
{
    private static GameCatalog Catalog(double perPenLevel = 0.01, double capMultiplier = 2.0)
        => new GameConfigBuilder().WithBeasts(b =>
        {
            b.TameChancePerPenLevel = perPenLevel;
            b.MaxTameChanceMultiplier = capMultiplier;
        }).BuildCatalog();

    private static double Chance(GameCatalog catalog, int penLevel)
        => new BeastTaming(catalog).ChanceFor(catalog.Beasts[TestKeys.Beast], penLevel);

    [Fact]
    public void ChanceFor_ShouldAddTheBonusPerPenLevel()
        => Assert.Equal(0.25, Chance(Catalog(), penLevel: 5), precision: 10);

    /// <summary>Високий звіринець не робить приручення гарантованим: стеля — подвоєна база.</summary>
    [Fact]
    public void ChanceFor_ShouldStopAtTheCap()
        => Assert.Equal(0.4, Chance(Catalog(), penLevel: 30), precision: 10);

    [Fact]
    public void ChanceFor_ShouldNeverExceedCertainty()
        => Assert.Equal(1.0, Chance(Catalog(perPenLevel: 0.5, capMultiplier: 10), penLevel: 30), precision: 10);

    [Fact]
    public void ForMonster_ShouldFindTheBeast_OnlyForItsMonster()
    {
        var taming = new BeastTaming(Catalog());

        Assert.Equal(TestKeys.Beast, taming.ForMonster(TestKeys.BeastMonster)?.Key);
        Assert.Null(taming.ForMonster("bats"));
    }

    /// <summary>Кожен наступний рівень дорожчий у ExperienceGrowth разів, з округленням угору.</summary>
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 125)]
    [InlineData(3, 157)]
    public void ExperienceToNext_ShouldGrowGeometrically(int level, int expected)
        => Assert.Equal(expected, new BeastProgression(Catalog()).ExperienceToNext(level));

    [Fact]
    public void MaxLevel_ShouldBeRankTimesLevelsPerRank()
        => Assert.Equal(30, new BeastProgression(Catalog()).MaxLevel(rank: 3));
}
