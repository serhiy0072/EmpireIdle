using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Validators;

namespace EmpireIdle.Application.Tests.Heroes;

/// <summary>Межі кількості уламків за одну покупку.</summary>
public class BuyHeroShardsCommandValidatorTests
{
    private readonly BuyHeroShardsCommandValidator _validator = new();

    private static BuyHeroShardsCommand Command(int count) => new(Guid.NewGuid(), "warrior_bran", count);

    [Theory]
    [InlineData(1)]
    [InlineData(BuyHeroShardsCommandValidator.MaxPerPurchase)]
    public void Validate_ShouldAccept_CountWithinBounds(int count)
    {
        Assert.True(_validator.Validate(Command(count)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(BuyHeroShardsCommandValidator.MaxPerPurchase + 1)]
    [InlineData(3_579_140)]
    public void Validate_ShouldReject_CountOutsideBounds(int count)
    {
        var result = _validator.Validate(Command(count));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BuyHeroShardsCommand.Count));
    }
}
