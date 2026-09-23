using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Переходи стану забігу. Завершений забіг незмінний — інакше нічия
/// після перемоги переписала б уже видану нагороду.
/// </summary>
public class DungeonRunTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static DungeonRun Run()
        => new(Guid.NewGuid(), Guid.NewGuid(), 1, "crypt", 1, "{}", Now);

    [Fact]
    public void TimeOut_ShouldFinishTheRunWithTheFinalBattle()
    {
        var run = Run();

        run.TimeOut("{\"round\":31}", Now.AddMinutes(5));

        Assert.Equal(DungeonRunState.TimedOut, run.State);
        Assert.Equal("{\"round\":31}", run.Battle);
        Assert.Equal(Now.AddMinutes(5), run.FinishedAt);
    }

    [Fact]
    public void TimeOut_AfterWin_ShouldThrow()
    {
        var run = Run();
        run.Win("{}", Now);

        Assert.Throws<InvalidStateException>(() => run.TimeOut("{}", Now));
    }
}
