using EmpireIdle.Application.Heroes.Commands;
using EmpireIdle.Application.Heroes.Validators;

namespace EmpireIdle.Application.Tests.Heroes;

public class BuyWeaponCommandValidatorTests
{
    private readonly BuyWeaponCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldAccept_AWellFormedCommand()
        => Assert.True(_validator.Validate(new BuyWeaponCommand(Guid.NewGuid(), "sword_iron")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_ShouldReject_EmptyKey(string key)
    {
        var result = _validator.Validate(new BuyWeaponCommand(Guid.NewGuid(), key));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BuyWeaponCommand.ItemKey));
    }

    [Fact]
    public void Validate_ShouldReject_TooLongKey()
    {
        var result = _validator.Validate(new BuyWeaponCommand(Guid.NewGuid(), new string('x', 51)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BuyWeaponCommand.ItemKey));
    }

    [Fact]
    public void Validate_ShouldReject_EmptyPlayer()
    {
        var result = _validator.Validate(new BuyWeaponCommand(Guid.Empty, "sword_iron"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BuyWeaponCommand.PlayerId));
    }
}
