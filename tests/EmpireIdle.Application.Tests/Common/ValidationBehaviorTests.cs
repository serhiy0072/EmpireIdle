using EmpireIdle.Application.Common.Behaviors;
using FluentValidation;
using MediatR;

namespace EmpireIdle.Application.Tests.Common;

/// <summary>
/// Валідатори запиту йдуть по черзі: async-правило може ходити в DbContext, а той не терпить
/// двох операцій одночасно. Кожен отримує свій контекст — помилки не дублюються.
/// </summary>
public class ValidationBehaviorTests
{
    public sealed record Probe(string Name) : IRequest<int>;

    /// <summary>Async-валідатор, що фіксує, скільки валідацій іде одночасно.</summary>
    private sealed class SlowValidator : AbstractValidator<Probe>
    {
        private static int _running;
        public static int MaxConcurrent;

        public SlowValidator(string message)
        {
            RuleFor(p => p.Name).MustAsync(async (_, ct) =>
            {
                MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref _running));
                await Task.Delay(20, ct);
                Interlocked.Decrement(ref _running);
                return false;
            }).WithMessage(message);
        }
    }

    [Fact]
    public async Task Handle_ShouldRunValidatorsOneAtATime_AndReportEachFailureOnce()
    {
        SlowValidator.MaxConcurrent = 0;
        var behavior = new ValidationBehavior<Probe, int>([new SlowValidator("first"), new SlowValidator("second")]);

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new Probe("x"), _ => Task.FromResult(1), CancellationToken.None));

        Assert.Equal(1, SlowValidator.MaxConcurrent);
        Assert.Equal(["first", "second"], error.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Handle_ShouldCallTheHandler_WhenThereAreNoValidators()
    {
        var behavior = new ValidationBehavior<Probe, int>([]);

        Assert.Equal(7, await behavior.Handle(new Probe("x"), _ => Task.FromResult(7), CancellationToken.None));
    }
}
