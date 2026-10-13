using EmpireIdle.Domain.Entities;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>Запас без стелі (GDD §4.1): переходить межу int і не загортається на межі long.</summary>
public class VillageResourceTests
{
    [Fact]
    public void Add_ShouldGoPastTheIntRange()
    {
        var resource = new VillageResource(Guid.NewGuid(), "food", int.MaxValue);

        resource.Add(1);

        Assert.Equal((long)int.MaxValue + 1, resource.Amount);
    }

    [Fact]
    public void Add_ShouldSaturateAtLongMax_InsteadOfWrapping()
    {
        var resource = new VillageResource(Guid.NewGuid(), "food", long.MaxValue - 1);

        resource.Add(10);

        Assert.Equal(long.MaxValue, resource.Amount);
    }

    [Fact]
    public void Subtract_ShouldTakeAnAmountBeyondTheIntRange()
    {
        var resource = new VillageResource(Guid.NewGuid(), "food", 5_000_000_000);

        resource.Subtract(3_000_000_000);

        Assert.Equal(2_000_000_000, resource.Amount);
    }
}
