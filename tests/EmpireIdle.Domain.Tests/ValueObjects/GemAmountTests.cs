using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Tests.ValueObjects;

/// <summary>Сума gems не перевертається в від'ємну: переповнення — помилка, а не мінус на балансі.</summary>
public class GemAmountTests
{
    [Fact]
    public void Add_ShouldSum() => Assert.Equal(15, new GemAmount(10).Add(new GemAmount(5)).Value);

    [Fact]
    public void Add_ShouldThrow_OnOverflow()
        => Assert.Throws<OverflowException>(() => new GemAmount(int.MaxValue).Add(new GemAmount(1)));
}
