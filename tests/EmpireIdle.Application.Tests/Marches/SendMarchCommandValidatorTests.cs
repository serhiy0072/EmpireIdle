using EmpireIdle.Application.Marches.Commands;
using EmpireIdle.Application.Marches.Validators;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>Підкріплення може йти самим героєм, атака — ні.</summary>
public class SendMarchCommandValidatorTests
{
    private static readonly SendMarchCommandValidator Validator = new();

    private static SendMarchCommand Command(MarchIntent intent, Dictionary<UnitStackKey, int> units) =>
        new(Guid.NewGuid(), MarchTargetType.Village, Guid.NewGuid(), units, Guid.NewGuid(), intent);

    [Fact]
    public void Validate_ShouldAccept_AHeroOnlyReinforcement()
    {
        var result = Validator.Validate(Command(MarchIntent.Reinforce, []));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReject_AnAttackWithoutUnits()
    {
        var result = Validator.Validate(Command(MarchIntent.Attack, []));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendMarchCommand.Units));
    }

    [Fact]
    public void Validate_ShouldAccept_AnAttackWithUnits()
    {
        var result = Validator.Validate(Command(MarchIntent.Attack, new() { [new UnitStackKey("infantry", 1)] = 5 }));

        Assert.True(result.IsValid);
    }
}
