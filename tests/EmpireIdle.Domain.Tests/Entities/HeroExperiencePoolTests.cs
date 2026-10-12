using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Досвід за монстрів — у межах денного ліміту перемог; нова доба UTC ліміт скидає.</summary>
public class HeroExperiencePoolTests
{
    private static readonly DateTime Morning = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);

    private static HeroExperiencePool Pool() => new(Guid.NewGuid(), Guid.NewGuid(), 1);

    [Fact]
    public void AddForMonster_ShouldAddTheExperience_WithinTheDailyLimit()
    {
        var pool = Pool();

        var first = pool.AddForMonster(1000, dailyLimit: 2, Morning);
        var second = pool.AddForMonster(3000, dailyLimit: 2, Morning.AddHours(1));

        Assert.Equal((1000, 3000), (first, second));
        Assert.Equal(4000, pool.Amount);
        Assert.Equal(2, pool.MonsterRewardsToday);
    }

    [Fact]
    public void AddForMonster_ShouldGiveNothing_OverTheDailyLimit()
    {
        var pool = Pool();
        pool.AddForMonster(1000, dailyLimit: 1, Morning);

        var granted = pool.AddForMonster(1000, dailyLimit: 1, Morning.AddHours(2));

        Assert.Equal(0, granted);
        Assert.Equal(1000, pool.Amount);
    }

    [Fact]
    public void AddForMonster_ShouldResetTheLimit_OnANewUtcDay()
    {
        var pool = Pool();
        pool.AddForMonster(1000, dailyLimit: 1, Morning);

        var granted = pool.AddForMonster(1000, dailyLimit: 1, Morning.Date.AddDays(1));

        Assert.Equal(1000, granted);
        Assert.Equal(2000, pool.Amount);
        Assert.Equal(1, pool.MonsterRewardsToday);
    }

    [Fact]
    public void AddForMonster_ShouldGiveNothing_WhenTheLimitIsZero()
    {
        var pool = Pool();

        Assert.Equal(0, pool.AddForMonster(1000, dailyLimit: 0, Morning));
        Assert.Equal(0, pool.Amount);
    }
}
