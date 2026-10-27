using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Validators;

namespace EmpireIdle.Application.Tests.Garrisons;

/// <summary>
/// Ціна прокачки сумує крок за кроком від FromLevel — рівні мають відсікатися
/// до обробника, інакше один запит з int.MinValue займає процесор на хвилини.
/// </summary>
public class LevelUpUnitsCommandValidatorTests
{
    private static bool IsValid(int fromLevel, int toLevel, int count)
        => new LevelUpUnitsCommandValidator()
            .Validate(new LevelUpUnitsCommand(Guid.NewGuid(), "infantry", fromLevel, toLevel, count))
            .IsValid;

    [Fact]
    public void Validate_ShouldAccept_AStepUp()
        => Assert.True(IsValid(fromLevel: 1, toLevel: 2, count: 1));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_ShouldReject_AFromLevelBelowOne(int fromLevel)
        => Assert.False(IsValid(fromLevel, toLevel: 2, count: 1));

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3, 2)]
    public void Validate_ShouldReject_ATargetNotAboveTheCurrentLevel(int fromLevel, int toLevel)
        => Assert.False(IsValid(fromLevel, toLevel, count: 1));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_ShouldReject_ANonPositiveCount(int count)
        => Assert.False(IsValid(fromLevel: 1, toLevel: 2, count));
}
