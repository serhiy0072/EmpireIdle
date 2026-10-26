using EmpireIdle.Application.Common;
using EmpireIdle.Application.Garrisons.Commands;
using EmpireIdle.Application.Garrisons.Validators;
using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Application.Tests.Garrisons;

/// <summary>
/// Кількість юнітів у рядку запиту має технічну стелю: два стеки по int.MaxValue інакше
/// переповнювали б суму в обробнику, і замість відмови гравець отримував би 500.
/// </summary>
public class UnitCountLimitValidatorTests
{
    private static Dictionary<UnitStackKey, int> Units(int count) => new() { [new UnitStackKey("infantry", 1)] = count };

    [Theory]
    [InlineData(UnitCountLimit.MaxPerStack, true)]
    [InlineData(int.MaxValue, false)]
    public void HealWounded_ShouldCapAStack(int count, bool valid)
        => Assert.Equal(valid, new HealWoundedCommandValidator()
            .Validate(new HealWoundedCommand(Guid.NewGuid(), Units(count), HealPaymentMethod.Resources)).IsValid);

    [Theory]
    [InlineData(UnitCountLimit.MaxPerStack, true)]
    [InlineData(int.MaxValue, false)]
    [InlineData(0, false)]
    public void RecoverUnits_ShouldCapAStack(int count, bool valid)
        => Assert.Equal(valid, new RecoverUnitsCommandValidator()
            .Validate(new RecoverUnitsCommand(Guid.NewGuid(), Units(count))).IsValid);
}
