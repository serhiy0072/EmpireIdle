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

    // ---------- Герой ----------

    [Fact]
    public void PutOnMarket_ShouldTakeTheHeroOutOfItsGarrisonAndLeadership()
    {
        var hero = TestKit.Entities.Hero(asLeader: true);

        hero.PutOnMarket(Now);

        Assert.Equal(HeroState.OnMarket, hero.State);
        Assert.Null(hero.StationedGarrisonId);
        Assert.False(hero.IsLeader);
    }

    /// <summary>Герой у поході чи поранений не виставляється.</summary>
    [Fact]
    public void PutOnMarket_ShouldRefuse_AHeroThatIsNotHomeAndIdle()
    {
        var hero = TestKit.Entities.Hero();
        hero.Deploy(Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => hero.PutOnMarket(Now));

        Assert.Equal(RefusalReasons.MarketHeroBusy.Key, refusal.Reason);
    }

    [Fact]
    public void TakeOffMarket_ShouldReturnTheHeroToTheGarrison()
    {
        var hero = TestKit.Entities.Hero();
        var garrison = Guid.NewGuid();
        hero.PutOnMarket(Now);

        hero.TakeOffMarket(garrison, leaderSlotFree: false, Now);

        Assert.Equal(HeroState.Idle, hero.State);
        Assert.Equal(garrison, hero.StationedGarrisonId);
        Assert.False(hero.IsLeader);
    }

    [Fact]
    public void SellTo_ShouldHandTheHeroToTheBuyerWithACooldown()
    {
        var hero = TestKit.Entities.Hero();
        var buyer = Guid.NewGuid();
        var garrison = Guid.NewGuid();
        hero.PutOnMarket(Now);

        hero.SellTo(buyer, garrison, leaderSlotFree: true, Now.AddHours(72), Now);

        Assert.Equal(buyer, hero.PlayerId);
        Assert.Equal(HeroState.Idle, hero.State);
        Assert.Equal(garrison, hero.StationedGarrisonId);
        Assert.True(hero.IsLeader);
        Assert.Equal(Now.AddHours(72), hero.ResaleLockedUntil);
    }

    /// <summary>Щойно куплений герой не перепродається до кінця кулдауну.</summary>
    [Fact]
    public void PutOnMarket_ShouldRefuse_ARecentlyBoughtHero()
    {
        var hero = TestKit.Entities.Hero();
        hero.PutOnMarket(Now);
        hero.SellTo(Guid.NewGuid(), Guid.NewGuid(), leaderSlotFree: false, Now.AddHours(72), Now);

        var refusal = Assert.Throws<RequirementNotMetException>(() => hero.PutOnMarket(Now.AddHours(71)));

        Assert.Equal(RefusalReasons.MarketResaleCooldown.Key, refusal.Reason);
        hero.PutOnMarket(Now.AddHours(72));
    }

    // ---------- Спорядження ----------

    /// <summary>Вдягнений предмет знімається з героя тут же.</summary>
    [Fact]
    public void PutOnMarket_ShouldUnequipAWornItem()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon);
        item.EquipTo(Guid.NewGuid(), 0, Now);

        item.PutOnMarket(Now);

        Assert.True(item.IsOnMarket);
        Assert.Null(item.EquippedByHeroId);
    }

    [Fact]
    public void PutOnMarket_ShouldRefuse_ABrokenItem()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon);
        item.Break(Now);

        var refusal = Assert.Throws<InvalidStateException>(() => item.PutOnMarket(Now));

        Assert.Equal(RefusalReasons.EquipmentBroken.Key, refusal.Reason);
    }

    /// <summary>Предмет у заставі не вдягається, не заточується й не виставляється вдруге.</summary>
    [Fact]
    public void AnItemOnTheMarket_ShouldRefuseToBeUsed()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon);
        item.PutOnMarket(Now);

        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.EquipTo(Guid.NewGuid(), 0, Now)).Reason);
        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.Enhance(Now)).Reason);
        Assert.Equal(RefusalReasons.MarketItemListed.Key,
            Assert.Throws<InvalidStateException>(() => item.PutOnMarket(Now)).Reason);
    }

    [Fact]
    public void SellTo_ShouldHandTheItemToTheBuyerWithACooldown()
    {
        var item = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon);
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
        var item = TestKit.Entities.Equipment(TestKeys.Weapon, EquipmentSlot.Weapon);
        item.PutOnMarket(Now);

        item.TakeOffMarket(Now);

        Assert.False(item.IsOnMarket);
        item.EquipTo(Guid.NewGuid(), 0, Now);
    }
}
