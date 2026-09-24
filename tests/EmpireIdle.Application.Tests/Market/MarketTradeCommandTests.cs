using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Market;

/// <summary>
/// Купівля, зняття й закінчення строку: товар і золото переходять
/// в одній транзакції, закритий лот не змінюється вдруге.
/// </summary>
public class MarketTradeCommandTests
{
    private readonly MarketTestBed _bed = new();

    private BuyMarketListingCommandHandler Buy() => new(
        _bed.MarketRepository, _bed.Villages, _bed.Goods, _bed.Desk, _bed.Catalog, _bed.UnitOfWork,
        new FakeTimeProvider(MarketTestBed.Now.AddHours(1)), NullLogger<BuyMarketListingCommandHandler>.Instance);

    private CancelMarketListingCommandHandler Cancel() => new(
        _bed.MarketRepository, _bed.Goods, _bed.UnitOfWork,
        new FakeTimeProvider(MarketTestBed.Now.AddHours(1)), NullLogger<CancelMarketListingCommandHandler>.Instance);

    private ExpireMarketListingCommandHandler Expire(DateTime now) => new(
        _bed.MarketRepository, _bed.Goods, _bed.UnitOfWork,
        new FakeTimeProvider(now), NullLogger<ExpireMarketListingCommandHandler>.Instance);

    /// <summary>Меч у заставі, виставлений за 100 золота.</summary>
    private (EquipmentItem Sword, MarketListing Listing) ListedSword()
    {
        var sword = _bed.GivenSword();
        sword.PutOnMarket(MarketTestBed.Now);

        return (sword, _bed.GivenListing(MarketListingKind.Equipment, equipmentId: sword.Id, price: 100));
    }

    // ---------- Купівля ----------

    [Fact]
    public async Task Buy_ShouldMoveTheItemAndTheGold()
    {
        var (sword, listing) = ListedSword();
        var buyerGold = _bed.Gold(_bed.BuyerVillage);
        var sellerGold = _bed.Gold(_bed.SellerVillage);

        await Buy().Handle(new BuyMarketListingCommand(_bed.Buyer, listing.Id), CancellationToken.None);

        Assert.Equal(MarketListingState.Sold, listing.State);
        Assert.Equal(_bed.Buyer, sword.PlayerId);
        Assert.False(sword.IsOnMarket);
        Assert.Equal(buyerGold - 100, _bed.Gold(_bed.BuyerVillage));
        Assert.Equal(sellerGold + 100, _bed.Gold(_bed.SellerVillage));
        await _bed.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Золота бракує — лот лишається активним, предмет у продавця.</summary>
    [Fact]
    public async Task Buy_ShouldRefuse_WithoutEnoughGold()
    {
        var bed = new MarketTestBed(gold: 50);
        var sword = bed.GivenSword();
        sword.PutOnMarket(MarketTestBed.Now);
        var listing = bed.GivenListing(MarketListingKind.Equipment, equipmentId: sword.Id, price: 100);

        var handler = new BuyMarketListingCommandHandler(bed.MarketRepository, bed.Villages, bed.Goods, bed.Desk, bed.Catalog,
            bed.UnitOfWork, new FakeTimeProvider(MarketTestBed.Now), NullLogger<BuyMarketListingCommandHandler>.Instance);

        await Assert.ThrowsAsync<NotEnoughResourcesException>(() =>
            handler.Handle(new BuyMarketListingCommand(bed.Buyer, listing.Id), CancellationToken.None));

        Assert.Equal(bed.Seller, sword.PlayerId);
        await bed.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Buy_ShouldRefuse_TheBuyersOwnListing()
    {
        var (_, listing) = ListedSword();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Buy().Handle(new BuyMarketListingCommand(_bed.Seller, listing.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketOwnListing.Key, refusal.Reason);
    }

    /// <summary>Героя, який уже є в покупця, купити не можна — інакше це обхід сузір'я.</summary>
    [Fact]
    public async Task Buy_ShouldRefuse_AHeroTheBuyerAlreadyHas()
    {
        var hero = _bed.GivenHero(_bed.Seller);
        hero.PutOnMarket(MarketTestBed.Now);
        _bed.GivenHero(_bed.Buyer);
        var listing = _bed.GivenListing(MarketListingKind.Hero, heroId: hero.Id, itemKey: hero.HeroKey);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Buy().Handle(new BuyMarketListingCommand(_bed.Buyer, listing.Id), CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketHeroAlreadyOwned.Key, refusal.Reason);
        await _bed.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Куплений герой стає в гарнізон покупця й отримує кулдаун перепродажу.</summary>
    [Fact]
    public async Task Buy_ShouldStationTheHeroInTheBuyersGarrison()
    {
        var hero = _bed.GivenHero(_bed.Seller);
        hero.PutOnMarket(MarketTestBed.Now);
        _bed.Heroes.GetByKeyAsync(_bed.Buyer, hero.HeroKey, Arg.Any<CancellationToken>()).Returns((Hero?)null);
        var listing = _bed.GivenListing(MarketListingKind.Hero, heroId: hero.Id, itemKey: hero.HeroKey);

        await Buy().Handle(new BuyMarketListingCommand(_bed.Buyer, listing.Id), CancellationToken.None);

        Assert.Equal(_bed.Buyer, hero.PlayerId);
        Assert.Equal(_bed.BuyerGarrison.Id, hero.StationedGarrisonId);
        Assert.True(hero.IsLeader);
        Assert.Equal(MarketTestBed.Now.AddHours(1 + _bed.Catalog.Config.Market.ResaleCooldownHours), hero.ResaleLockedUntil);
    }

    /// <summary>Пачка переходить у стек покупця.</summary>
    [Fact]
    public async Task Buy_ShouldGrantTheStackToTheBuyer()
    {
        var buyerStack = _bed.GivenTonics(_bed.Buyer, count: 1);
        var listing = _bed.GivenListing(MarketListingKind.Item, itemKey: MarketTestBed.Tonic, quantity: 3, units: 3, price: 1500);

        await Buy().Handle(new BuyMarketListingCommand(_bed.Buyer, listing.Id), CancellationToken.None);

        Assert.Equal(4, buyerStack.Count);
    }

    // ---------- Зняття й строк ----------

    [Fact]
    public async Task Cancel_ShouldReturnTheItem()
    {
        var (sword, listing) = ListedSword();

        await Cancel().Handle(new CancelMarketListingCommand(_bed.Seller, listing.Id), CancellationToken.None);

        Assert.Equal(MarketListingState.Cancelled, listing.State);
        Assert.False(sword.IsOnMarket);
    }

    /// <summary>Чужий лот не відрізняється від неіснуючого.</summary>
    [Fact]
    public async Task Cancel_ShouldNotTouch_SomeoneElsesListing()
    {
        var (_, listing) = ListedSword();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Cancel().Handle(new CancelMarketListingCommand(_bed.Buyer, listing.Id), CancellationToken.None));

        Assert.Equal(MarketListingState.Active, listing.State);
    }

    /// <summary>Герой повертається в гарнізон продавця; без лідера — стає лідером.</summary>
    [Fact]
    public async Task Cancel_ShouldReturnTheHeroHome()
    {
        var hero = _bed.GivenHero(_bed.Seller);
        hero.PutOnMarket(MarketTestBed.Now);
        var listing = _bed.GivenListing(MarketListingKind.Hero, heroId: hero.Id, itemKey: hero.HeroKey);

        await Cancel().Handle(new CancelMarketListingCommand(_bed.Seller, listing.Id), CancellationToken.None);

        Assert.Equal(HeroState.Idle, hero.State);
        Assert.Equal(_bed.SellerGarrison.Id, hero.StationedGarrisonId);
    }

    [Fact]
    public async Task Expire_ShouldReturnTheStackToTheSeller()
    {
        var stack = _bed.GivenTonics(_bed.Seller, count: 1);
        var listing = _bed.GivenListing(MarketListingKind.Item, itemKey: MarketTestBed.Tonic, quantity: 3, units: 3, price: 1500);

        await Expire(listing.ExpiresAt).Handle(new ExpireMarketListingCommand(listing.Id), CancellationToken.None);

        Assert.Equal(MarketListingState.Expired, listing.State);
        Assert.Equal(4, stack.Count);
    }

    /// <summary>Лот, куплений між вибіркою сканера й обробкою, сканер не чіпає.</summary>
    [Fact]
    public async Task Expire_ShouldSkip_AListingThatWasSoldMeanwhile()
    {
        var (_, listing) = ListedSword();
        listing.Buy(_bed.Buyer, MarketTestBed.Now);

        await Expire(listing.ExpiresAt).Handle(new ExpireMarketListingCommand(listing.Id), CancellationToken.None);

        Assert.Equal(MarketListingState.Sold, listing.State);
        await _bed.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
