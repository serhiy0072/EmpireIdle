using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Validators;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>
/// До 80-го рівня артефакта потрібні сотні звичайних предметів — «макс.» у кузні не має впиратися в ліміт запиту.
/// </summary>
public class FeedArtifactCommandValidatorTests
{
    private static FeedArtifactCommand Command(int food)
        => new(Guid.NewGuid(), Guid.NewGuid(), Enumerable.Range(0, food).Select(_ => Guid.NewGuid()).ToList(),
            new Dictionary<string, int>());

    [Theory]
    [InlineData(101, true)]
    [InlineData(FeedArtifactCommand.MaxFood, true)]
    [InlineData(FeedArtifactCommand.MaxFood + 1, false)]
    public void Validate_ShouldTakeHundredsOfFoodItems(int food, bool valid)
        => Assert.Equal(valid, new FeedArtifactCommandValidator().Validate(Command(food)).IsValid);

    [Fact]
    public void Validate_ShouldRejectAnEmptyFeed()
        => Assert.False(new FeedArtifactCommandValidator().Validate(Command(0)).IsValid);
}
