using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Списання з множником. Добуток рахувався в int і загортався:
/// 1200 × 3 579 140 ставало 704.
/// </summary>
public class VillageChargeCostTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Village VillageWithGold(int gold)
    {
        var village = new Village(Guid.NewGuid(), Guid.NewGuid(), "Test Village", ["gold"], 0, 0);
        village.GrantStartingResources(new Dictionary<string, int> { ["gold"] = gold }, Now);
        return village;
    }

    private static int Gold(Village village)
        => village.Resources.Single(r => r.ResourceType == "gold").Amount;

    private static List<ResourceCost> Price(int amount)
        => [new ResourceCost { Resource = "gold", Amount = amount }];

    [Fact]
    public void ChargeCost_ShouldReject_WhenTheProductWrapsToSmallPositive()
    {
        var village = VillageWithGold(1_000);

        // 1200 × 3 579 140 = 2^32 + 704
        Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(Price(1_200), Now, 3_579_140));

        Assert.Equal(1_000, Gold(village));
    }

    [Fact]
    public void ChargeCost_ShouldReject_WhenTheProductWrapsToNegative()
    {
        var village = VillageWithGold(1_000);

        // 100 × 21 474 837 > int.MaxValue
        Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(Price(100), Now, 21_474_837));

        Assert.Equal(1_000, Gold(village));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ChargeCost_ShouldReject_NonPositiveMultiplier(int multiplier)
    {
        var village = VillageWithGold(1_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => village.ChargeCost(Price(100), Now, multiplier));

        Assert.Equal(1_000, Gold(village));
    }

    [Fact]
    public void ChargeCost_ShouldChargeAmountTimesMultiplier()
    {
        var village = VillageWithGold(1_000);

        village.ChargeCost(Price(120), Now, 5);

        Assert.Equal(400, Gold(village));
    }

    [Fact]
    public void ChargeCost_ShouldSumRepeatedLines_BeforeCharging()
    {
        var village = VillageWithGold(150);
        List<ResourceCost> cost =
        [
            new ResourceCost { Resource = "gold", Amount = 100 },
            new ResourceCost { Resource = "gold", Amount = 100 }
        ];

        Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(cost, Now));

        Assert.Equal(150, Gold(village));
    }

    [Fact]
    public void ChargeCost_ShouldChargeRepeatedLinesTogether()
    {
        var village = VillageWithGold(500);
        List<ResourceCost> cost =
        [
            new ResourceCost { Resource = "gold", Amount = 100 },
            new ResourceCost { Resource = "gold", Amount = 50 }
        ];

        village.ChargeCost(cost, Now, multiplier: 2);

        Assert.Equal(200, Gold(village));
    }

    [Fact]
    public void ChargeCost_ShouldReject_NegativeAmount_WithoutTouchingStock()
    {
        var village = VillageWithGold(1_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => village.ChargeCost(Price(-100), Now));

        Assert.Equal(1_000, Gold(village));
    }

    /// <summary>3 × int.MaxValue × int.MaxValue не влазить у long.</summary>
    [Fact]
    public void ChargeCost_ShouldReject_WhenTheSummedProductExceedsLong()
    {
        var village = VillageWithGold(1_000);
        List<ResourceCost> cost =
        [
            new ResourceCost { Resource = "gold", Amount = int.MaxValue },
            new ResourceCost { Resource = "gold", Amount = int.MaxValue },
            new ResourceCost { Resource = "gold", Amount = int.MaxValue }
        ];

        Assert.Throws<NotEnoughResourcesException>(() => village.ChargeCost(cost, Now, int.MaxValue));

        Assert.Equal(1_000, Gold(village));
    }
}
