using EmpireIdle.Infrastructure.Persistence.Outbox;

namespace EmpireIdle.Api.Tests.Infrastructure;

/// <summary>
/// Подія, що впала, повторюється з паузою, яка подвоюється: короткий збій залежності
/// не спалює всі спроби за пів хвилини, а довгий не крутить воркер вхолосту.
/// </summary>
public class OutboxRetryDelayTests
{
    private static readonly OutboxSettings Settings = new() { RetryBaseSeconds = 5, RetryMaxSeconds = 3600 };

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(5, 80)]
    public void RetryDelay_ShouldDoubleWithEveryFailure(int failedAttempts, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), OutboxProcessor.RetryDelay(failedAttempts, Settings));

    [Fact]
    public void RetryDelay_ShouldStopAtTheCeiling()
        => Assert.Equal(TimeSpan.FromHours(1), OutboxProcessor.RetryDelay(30, Settings));
}
