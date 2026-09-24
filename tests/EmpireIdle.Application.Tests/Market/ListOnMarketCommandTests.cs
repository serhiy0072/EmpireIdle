using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Market;

/// <summary>
/// Виставлення на ринок: ринок відкритий, лот вільний, ціна в коридорі,
/// податок списаний, товар у заставі.
/// </summary>
public class ListOnMarketCommandTests
{
    private readonly MarketTestBed _bed = new();

    private ListOnMarketCommandHandler Handler() => new(
        _bed.Villages, _bed.MarketRepository, _bed.Goods, _bed.Desk, _bed.Catalog, _bed.ServerContext,
        _bed.UnitOfWork, new FakeTimeProvider(MarketTestBed.Now), NullLogger<ListOnMarketCommandHandler>.Instance);

    private Task<Guid> ListSword(Guid swordId, int price)
        => Handler().Handle(new ListOnMarketCommand(_bed.Seller, MarketListingKind.Equipment, swordId, null, null, 1, price),
            CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldListEquipment_PutItInEscrowAndChargeTheTax()
    {
        var sword = _bed.GivenSword();
        var before = _bed.Gold(_bed.SellerVillage);

        var id = await ListSword(sword.Id, 100);

        var listing = Assert.Single(_bed.Added);
        Assert.Equal(id, listing.Id);
        Assert.Equal(10, listing.Units, 3);
        Assert.Equal("weapon", listing.PricingKey);
        Assert.True(sword.IsOnMarket);

        // 5% від 100
        Assert.Equal(before - 5, _bed.Gold(_bed.SellerVillage));
        await _bed.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Поки ратуша не доросла до будівлі ринку, ринок закритий.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhileTheMarketIsUnderTheFog()
    {
        var bed = new MarketTestBed(townHallLevel: 1);
        var sword = bed.GivenSword();
        var handler = new ListOnMarketCommandHandler(bed.Villages, bed.MarketRepository, bed.Goods, bed.Desk, bed.Catalog,
            bed.ServerContext, bed.UnitOfWork, new FakeTimeProvider(MarketTestBed.Now), NullLogger<ListOnMarketCommandHandler>.Instance);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => handler.Handle(
            new ListOnMarketCommand(bed.Seller, MarketListingKind.Equipment, sword.Id, null, null, 1, 100), CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketLocked.Key, refusal.Reason);
        Assert.Equal(MarketTestBed.MarketOpensAt, refusal.Args["level"]);
        Assert.False(sword.IsOnMarket);
    }

    /// <summary>Ціна поза коридором — відмова з межами для всього лота.</summary>
    [Theory]
    [InlineData(69)]
    [InlineData(131)]
    public async Task Handle_ShouldRefuse_APriceOutsideTheCorridor(int price)
    {
        var sword = _bed.GivenSword();

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => ListSword(sword.Id, price));

        Assert.Equal(RefusalReasons.MarketPriceOutOfCorridor.Key, refusal.Reason);
        Assert.Equal(70, refusal.Args["min"]);
        Assert.Equal(130, refusal.Args["max"]);
        Assert.False(sword.IsOnMarket);
        Assert.Empty(_bed.Added);
    }

    /// <summary>Ринок 1 рівня дозволяє два лоти: базовий плюс один за рівень.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheListingLimitIsReached()
    {
        var sword = _bed.GivenSword();
        _bed.MarketRepository.CountActiveAsync(_bed.Seller, Arg.Any<CancellationToken>()).Returns(2);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => ListSword(sword.Id, 100));

        Assert.Equal(RefusalReasons.MarketListingLimit.Key, refusal.Reason);
        Assert.Equal(2, refusal.Args["limit"]);
    }

    /// <summary>Чужий предмет не відрізняється від неіснуючого.</summary>
    [Fact]
    public async Task Handle_ShouldNotList_SomeoneElsesItem()
    {
        var sword = _bed.GivenSword(owner: _bed.Buyer);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => ListSword(sword.Id, 100));
    }

    /// <summary>Стаковий товар знімається з інвентаря пачкою; одиниця ціни — штука.</summary>
    [Fact]
    public async Task Handle_ShouldTakeAStackOfTradeableItems()
    {
        var stack = _bed.GivenTonics(_bed.Seller, count: 10);

        // Якір: 5 gems × 100 = 500 за штуку, коридор на 4 штуки — 1400–2600
        await Handler().Handle(new ListOnMarketCommand(_bed.Seller, MarketListingKind.Item, null, null, MarketTestBed.Tonic, 4, 2000),
            CancellationToken.None);

        Assert.Equal(6, stack.Count);
        var listing = Assert.Single(_bed.Added);
        Assert.Equal(4, listing.Quantity);
        Assert.Equal("item.tonic", listing.PricingKey);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_MoreItemsThanThePlayerHas()
    {
        _bed.GivenTonics(_bed.Seller, count: 2);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(
            new ListOnMarketCommand(_bed.Seller, MarketListingKind.Item, null, null, MarketTestBed.Tonic, 4, 2000),
            CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketNotEnoughItems.Key, refusal.Reason);
    }

    /// <summary>Предмет без позначки Tradeable не продається — навіть якщо він у гравця є.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AnItemThatIsNotTradeable()
    {
        var essence = _bed.Catalog.Config.Items.First(i => i.Type == "evolution");

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(
            new ListOnMarketCommand(_bed.Seller, MarketListingKind.Item, null, null, essence.Key, 1, 100),
            CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketNotTradeable.Key, refusal.Reason);
    }

    /// <summary>Герой іде на ринок без гарнізону й лідерства; одиниця ціни — його Power.</summary>
    [Fact]
    public async Task Handle_ShouldListAHeroAtItsPower()
    {
        var hero = _bed.GivenHero(_bed.Seller);
        var power = _bed.HeroStats.Power(hero, _bed.Catalog.Hero(hero.HeroKey));
        var price = (int)(power * 10);

        await Handler().Handle(new ListOnMarketCommand(_bed.Seller, MarketListingKind.Hero, null, hero.Id, null, 1, price),
            CancellationToken.None);

        Assert.Equal(HeroState.OnMarket, hero.State);
        Assert.Equal(power, Assert.Single(_bed.Added).Units, 3);
    }

    /// <summary>Герой, що тренується, не виставляється: лот продавався б за стару силу.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_AHeroInTraining()
    {
        var hero = _bed.GivenHero(_bed.Seller);
        var power = _bed.HeroStats.Power(hero, _bed.Catalog.Hero(hero.HeroKey));

        _bed.Heroes.GetActiveOrderAsync(_bed.Seller, Arg.Any<CancellationToken>())
            .Returns(new Domain.Entities.HeroLevelOrder(Guid.NewGuid(), hero.Id, _bed.Seller, 1, 2, MarketTestBed.Now.AddHours(1)));

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => Handler().Handle(
            new ListOnMarketCommand(_bed.Seller, MarketListingKind.Hero, null, hero.Id, null, 1, (int)(power * 10)),
            CancellationToken.None));

        Assert.Equal(RefusalReasons.MarketHeroTraining.Key, refusal.Reason);
        Assert.Equal(HeroState.Idle, hero.State);
    }
}
