using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.TestKit;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Застава ринку: поки лот відкритий, товар не можна ні використати,
/// ні виставити вдруге. Продаж переносить його до покупця з кулдауном.
/// </summary>
public class MarketCustodyTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;

    // ---------- Спорядження ----------

    /// <summary>Вдягнений предмет знімається з героя тут же.</summary>
    [Fact]
    public void PutOnMarket_ShouldUnequipAWornItem()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact);
        item.EquipTo(Guid.NewGuid(), 0, Now);

        item.PutOnMarket(Now);

        Assert.True(item.IsOnMarket);
        Assert.Null(item.EquippedByHeroId);
    }

    /// <summary>Предмет у заставі не вдягається, не прокачується й не виставляється вдруге.</summary>
    [Fact]
    public void AnItemOnTheMarket_ShouldRefuseToBeUsed()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact);
        item.PutOnMarket(Now);

        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.EquipTo(Guid.NewGuid(), 0, Now)).Reason);
        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.GainExperience(40, 1, Now)).Reason);
        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.RaiseMastery(Now)).Reason);
        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.PutOnMarket(Now)).Reason);
    }

    [Fact]
    public void SellTo_ShouldHandTheItemToTheBuyerWithACooldown()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact);
        var buyer = Guid.NewGuid();
        item.PutOnMarket(Now);

        item.SellTo(buyer, Now.AddHours(72), Now);

        Assert.Equal(buyer, item.PlayerId);
        Assert.False(item.IsOnMarket);
        Assert.Equal(RefusalReasons.MarketResaleCooldown.Key,
            Assert.Throws<RequirementNotMetException>(() => item.PutOnMarket(Now.AddHours(1))).Reason);
    }

    [Fact]
    public void TakeOffMarket_ShouldFreeTheItem()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Artifact, EquipmentSlot.Artifact);
        item.PutOnMarket(Now);

        item.TakeOffMarket(Now);

        Assert.False(item.IsOnMarket);
        item.EquipTo(Guid.NewGuid(), 0, Now);
    }
}
