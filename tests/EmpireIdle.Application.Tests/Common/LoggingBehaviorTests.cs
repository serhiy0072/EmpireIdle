using EmpireIdle.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>Один запис на запит: успіх — Information, відмова — Warning з типом винятку.</summary>
public class LoggingBehaviorTests
{
    public sealed record Probe : IRequest<int>;

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task Handle_ShouldWriteASingleEntry_OnSuccess()
    {
        var logger = new ListLogger<LoggingBehavior<Probe, int>>();

        await new LoggingBehavior<Probe, int>(logger).Handle(new Probe(), _ => Task.FromResult(1), CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.StartsWith("Handled Probe in", entry.Message);
    }

    [Fact]
    public async Task Handle_ShouldLogTheFailure_AndRethrow()
    {
        var logger = new ListLogger<LoggingBehavior<Probe, int>>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoggingBehavior<Probe, int>(logger)
            .Handle(new Probe(), _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("InvalidOperationException", entry.Message);
    }
}
