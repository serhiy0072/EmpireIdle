using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Domain.Tests.Entities;

public class BannerPityProgressTests
{
    private static BannerPityProgress Progress()
        => new(Guid.NewGuid(), Guid.NewGuid(), "hero");

    [Fact]
    public void New_ShouldStartEmpty()
    {
        var progress = Progress();

        Assert.Equal(PityState.Empty, progress.State);
        Assert.Equal(0, progress.TotalRolls);
    }

    [Fact]
    public void Apply_ShouldStoreTheStateAndCountTheRoll()
    {
        var progress = Progress();

        progress.Apply(new PityState(RareSince: 3, UniqueSince: 12, FeaturedGuaranteed: true));

        Assert.Equal(3, progress.RareSince);
        Assert.Equal(12, progress.UniqueSince);
        Assert.True(progress.FeaturedGuaranteed);
        Assert.Equal(1, progress.TotalRolls);
    }

    [Fact]
    public void Apply_ShouldKeepCountingRolls_WhenTheCountersReset()
    {
        var progress = Progress();

        progress.Apply(new PityState(1, 1, false));
        progress.Apply(new PityState(0, 2, false));

        Assert.Equal(0, progress.RareSince);
        Assert.Equal(2, progress.UniqueSince);
        Assert.Equal(2, progress.TotalRolls);
    }
}
