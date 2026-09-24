using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.ValueObjects;

public class UnitStackKeyTests
{
    [Fact]
    public void Parse_ShouldSplitTypeAndLevel()
    {
        Assert.Equal(new UnitStackKey("infantry", 10), UnitStackKey.Parse("infantry@10"));
    }

    /// <summary>
    /// Ключ приходить із клієнта. Кривий ключ має ставати 400, а не 500:
    /// ArgumentException глобальний обробник віддає як InvalidArgument.
    /// </summary>
    [Theory]
    [InlineData("infantry")]
    [InlineData("infantry@")]
    [InlineData("infantry@ten")]
    [InlineData("@5")]
    [InlineData("")]
    public void Parse_ShouldThrowArgumentException_ForMalformedKey(string key)
    {
        Assert.Throws<ArgumentException>(() => UnitStackKey.Parse(key));
    }
}
