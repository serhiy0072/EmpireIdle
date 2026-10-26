using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Validators;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Назву зберігають після Trim — довжина мірається так само, і кратні пробіли не проходять.</summary>
public class CreateClanCommandValidatorTests
{
    [Theory]
    [InlineData("Вовки", true)]
    [InlineData("Сірі вовки", true)]
    [InlineData(" ab", false)]
    [InlineData("ab ", false)]
    [InlineData("Сірі  вовки", false)]
    public void Validate_ShouldMeasureTheNameAsItIsStored(string name, bool valid)
        => Assert.Equal(valid, new CreateClanCommandValidator().Validate(new CreateClanCommand(Guid.NewGuid(), name, "WLF")).IsValid);
}
