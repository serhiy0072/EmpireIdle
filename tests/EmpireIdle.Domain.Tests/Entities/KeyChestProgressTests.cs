using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Безкоштовні відкриття ключової скрині: денний ліміт за добою UTC і перезарядка між відкриттями.
/// Рідкісна — 5 на добу з перезарядкою 5 хв, унікальна — 1 на добу з перезарядкою 48 год.
/// </summary>
public class KeyChestProgressTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static KeyChestProgress Progress() => new(Guid.NewGuid(), Guid.NewGuid(), "chest_key_rare");

    [Fact]
    public void NextFreeAt_ShouldBeNow_ForAChestNeverOpened()
        => Assert.Null(Progress().NextFreeAt(perDay: 5, cooldownMinutes: 5, Now));

    /// <summary>Після відкриття — чекати перезарядку.</summary>
    [Fact]
    public void NextFreeAt_ShouldWaitForTheCooldown_AfterAFreeOpening()
    {
        var progress = Progress();
        progress.UseFree(perDay: 5, cooldownMinutes: 5, Now);

        Assert.Equal(Now.AddMinutes(5), progress.NextFreeAt(5, 5, Now.AddMinutes(1)));
        Assert.Null(progress.NextFreeAt(5, 5, Now.AddMinutes(5)));
    }

    /// <summary>Вичерпана доба — наступне з опівночі UTC.</summary>
    [Fact]
    public void NextFreeAt_ShouldWaitForMidnight_WhenTheDayIsUsedUp()
    {
        var progress = Progress();

        for (var i = 0; i < 5; i++)
            progress.UseFree(perDay: 5, cooldownMinutes: 5, Now.AddMinutes(5 * i));

        Assert.Equal(0, 5 - progress.FreeUsedToday(Now.AddHours(1)));
        Assert.Equal(Now.Date.AddDays(1), progress.NextFreeAt(5, 5, Now.AddHours(1)));
        Assert.Null(progress.NextFreeAt(5, 5, Now.Date.AddDays(1)));
    }

    /// <summary>Унікальна: одна на добу з перезарядкою 48 год — тобто раз на два дні, не з опівночі.</summary>
    [Fact]
    public void NextFreeAt_ShouldWaitTwoDays_ForTheUniqueChest()
    {
        var progress = Progress();
        progress.UseFree(perDay: 1, cooldownMinutes: 2_880, Now);

        Assert.Equal(Now.AddDays(2), progress.NextFreeAt(1, 2_880, Now.Date.AddDays(1).AddMinutes(1)));
        Assert.Null(progress.NextFreeAt(1, 2_880, Now.AddDays(2)));
    }

    /// <summary>Не готове — відмова з моментом, коли буде.</summary>
    [Fact]
    public void UseFree_ShouldRefuse_BeforeTheCooldownEnds()
    {
        var progress = Progress();
        progress.UseFree(perDay: 5, cooldownMinutes: 5, Now);

        var refusal = Assert.Throws<InvalidStateException>(() => progress.UseFree(5, 5, Now.AddMinutes(2)));

        Assert.Equal(RefusalReasons.KeyChestFreeNotReady.Key, refusal.Reason);
        Assert.Equal(1, progress.FreeUsedToday(Now.AddMinutes(2)));
    }

    /// <summary>Скриня без безкоштовних відкриттів — лише ключем.</summary>
    [Fact]
    public void UseFree_ShouldRefuse_WhenTheChestHasNoFreeOpenings()
        => Assert.Throws<InvalidStateException>(() => Progress().UseFree(perDay: 0, cooldownMinutes: 0, Now));
}
