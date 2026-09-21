using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

public class TutorialProgressTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static TutorialProgress Progress() => new(Guid.NewGuid(), Guid.NewGuid(), Now);

    [Fact]
    public void MarkSeen_ShouldRecordTheStepOnce()
    {
        var progress = Progress();

        Assert.True(progress.MarkSeen("intro.collect", Now));
        Assert.False(progress.MarkSeen("intro.collect", Now.AddMinutes(1)));

        Assert.Equal(["intro.collect"], progress.SeenSteps);
        // Повтор не рухає UpdatedAt: зайвий UPDATE лише зіштовхнув би сусідню вкладку
        Assert.Equal(Now, progress.UpdatedAt);
    }

    [Fact]
    public void MarkSeen_ShouldKeepTheOrderOfDiscovery()
    {
        var progress = Progress();

        progress.MarkSeen("intro.collect", Now);
        progress.MarkSeen("intro.claim", Now);

        Assert.Equal(["intro.collect", "intro.claim"], progress.SeenSteps);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkSeen_ShouldRejectABlankKey(string key)
    {
        Assert.Throws<InvalidStateException>(() => Progress().MarkSeen(key, Now));
    }

    [Fact]
    public void MarkSeen_ShouldRejectAKeyLongerThanTheCap()
    {
        var key = new string('a', TutorialProgress.MaxStepKeyLength + 1);

        Assert.Throws<InvalidStateException>(() => Progress().MarkSeen(key, Now));
    }

    [Fact]
    public void Skip_ShouldStampOnce_AndKeepSeenSteps()
    {
        var progress = Progress();
        progress.MarkSeen("intro.collect", Now);

        progress.Skip(Now.AddMinutes(1));
        progress.Skip(Now.AddMinutes(2));

        Assert.Equal(Now.AddMinutes(1), progress.SkippedAt);
        Assert.Single(progress.SeenSteps);
    }
}
