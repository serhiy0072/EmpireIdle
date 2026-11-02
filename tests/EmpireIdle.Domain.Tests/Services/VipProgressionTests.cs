using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Services;

/// <summary>VIP-рівні за порогами куплених gems і додаткові марші на кожному парному рівні (рішення 28.10.2026).</summary>
public class VipProgressionTests
{
    private static readonly VipProgression Vip = new(new MonetizationConfig
    {
        VipThresholds = [2500, 7500, 15000, 25000],
        VipLevelsPerExtraMarch = 2,
    });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2499, 0)]
    [InlineData(2500, 1)]
    [InlineData(7499, 1)]
    [InlineData(15000, 3)]
    [InlineData(1_000_000, 4)]
    public void Level_ShouldCountReachedThresholds(long purchased, int expected)
        => Assert.Equal(expected, Vip.Level(purchased));

    [Theory]
    [InlineData(0, 2500L)]
    [InlineData(1, 7500L)]
    [InlineData(4, null)]
    public void NextThreshold_ShouldPointAtTheNextLevel(int level, long? expected)
        => Assert.Equal(expected, Vip.NextThreshold(level));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void ExtraMarches_ShouldGrowOnEveryEvenLevel(int level, int expected)
        => Assert.Equal(expected, Vip.ExtraMarches(level));

    /// <summary>Без порогів у конфігу VIP вимкнено: будь-яка сума — нульовий рівень.</summary>
    [Fact]
    public void Level_ShouldBeZero_WhenNoThresholdsAreConfigured()
        => Assert.Equal(0, new VipProgression(new MonetizationConfig()).Level(1_000_000));
}
