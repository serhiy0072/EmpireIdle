using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Особисті технології гравця: одне дослідження за раз, скорочення таймера й запис рівня.</summary>
public class PlayerResearchTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TechnologyKey Attack = new(1, "attack_infantry");

    private static PlayerResearch Research() => new(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

    [Fact]
    public void Start_ShouldResearchTheNextLevel()
    {
        var research = Research();

        research.Start(Attack, TimeSpan.FromMinutes(30), Now);

        Assert.Equal(("1:attack_infantry", 1, Now.AddMinutes(30)), (research.ActiveKey, research.ActiveLevel, research.CompletesAt));
    }

    /// <summary>Друге дослідження поверх поточного — чесна відмова, а не тихий перезапуск таймера.</summary>
    [Fact]
    public void Start_ShouldRefuse_WhileAnotherResearchRuns()
    {
        var research = Research();
        research.Start(Attack, TimeSpan.FromMinutes(30), Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() =>
            research.Start(new TechnologyKey(1, "attack_archer"), TimeSpan.FromMinutes(5), Now));

        Assert.Equal(RefusalReasons.ResearchBusy.Key, refusal.Reason);
    }

    [Fact]
    public void CompleteDue_ShouldWaitForTheTimer_ThenRecordTheLevelAndFreeTheAcademy()
    {
        var research = Research();
        research.Start(Attack, TimeSpan.FromMinutes(30), Now);

        Assert.Null(research.CompleteDue(Now.AddMinutes(29)));

        Assert.Equal(Attack, research.CompleteDue(Now.AddMinutes(30)));
        Assert.Equal(1, research.LevelOf(Attack));
        Assert.False(research.IsResearching);
    }

    /// <summary>Наступне дослідження того ж вузла дає вже другий рівень.</summary>
    [Fact]
    public void Start_ShouldContinueFromTheResearchedLevel()
    {
        var research = Research();
        research.Start(Attack, TimeSpan.Zero, Now);
        research.CompleteDue(Now);

        research.Start(Attack, TimeSpan.FromMinutes(10), Now);
        research.CompleteDue(Now.AddMinutes(10));

        Assert.Equal(2, research.LevelOf(Attack));
    }

    /// <summary>Прискорення не відсуває кінець у минуле: дозріле завершить сканер або наступна дія.</summary>
    [Fact]
    public void Shorten_ShouldCutTheRemainder_ButNotBeforeNow()
    {
        var research = Research();
        research.Start(Attack, TimeSpan.FromMinutes(30), Now);

        research.Shorten(TimeSpan.FromMinutes(10), Now);
        Assert.Equal(TimeSpan.FromMinutes(20), research.Remaining(Now));

        research.Shorten(TimeSpan.FromHours(5), Now);
        Assert.Equal(Now, research.CompletesAt);
    }
}
