using EmpireIdle.Application.Common.Events;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>
/// Пуші з обробників outbox чекають коміту: до нього нічого не летить, після — усе по черзі,
/// а на відкаті зібране відкидається. У звичайному запиті пуш іде одразу.
/// </summary>
public class NotificationBufferTests
{
    private readonly List<string> _sent = [];

    private Func<Task> Push(string name) => () =>
    {
        _sent.Add(name);
        return Task.CompletedTask;
    };

    [Fact]
    public async Task Send_ShouldGoImmediately_WhenNotDeferring()
    {
        var buffer = new NotificationBuffer();

        await buffer.SendAsync(Push("a"));

        Assert.Equal(["a"], _sent);
    }

    [Fact]
    public async Task Send_ShouldWaitForTheFlush_WhileDeferring()
    {
        var buffer = new NotificationBuffer();
        buffer.Defer();

        await buffer.SendAsync(Push("a"));
        await buffer.SendAsync(Push("b"));
        Assert.Empty(_sent);

        Assert.Empty(await buffer.FlushAsync());
        Assert.Equal(["a", "b"], _sent);
        Assert.False(buffer.IsDeferring);
    }

    [Fact]
    public async Task Discard_ShouldDropThePendingPushes()
    {
        var buffer = new NotificationBuffer();
        buffer.Defer();
        await buffer.SendAsync(Push("a"));

        buffer.Discard();
        await buffer.FlushAsync();

        Assert.Empty(_sent);
    }

    /// <summary>Стан уже закомічено: збій одного пушу не зупиняє решту, а повертається для логу.</summary>
    [Fact]
    public async Task Flush_ShouldKeepSending_AfterAPushFails()
    {
        var buffer = new NotificationBuffer();
        buffer.Defer();
        await buffer.SendAsync(() => throw new InvalidOperationException("hub down"));
        await buffer.SendAsync(Push("b"));

        var failures = await buffer.FlushAsync();

        Assert.IsType<InvalidOperationException>(Assert.Single(failures));
        Assert.Equal(["b"], _sent);
    }
}
