using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;

namespace EmpireIdle.Domain.Tests.Entities;

/// <summary>
/// Лот ринку: купівля, зняття й закінчення строку. Закритий лот
/// не змінюється вже ніколи — інакше один предмет продався б двічі.
/// </summary>
public class MarketListingTests
{
    private static readonly DateTime Now = TestKit.Entities.Now;
    private static readonly Guid Seller = Guid.NewGuid();

    private static MarketListing Listing(int price = 1000, double units = 50)
        => new(Guid.NewGuid(), 1, Seller, MarketListingKind.Item, null, null, "boost", 5, units,
            "item.boost", price, taxGold: 50, Now, TimeSpan.FromHours(48));

    [Fact]
    public void Constructor_ShouldOpenTheListingForItsDuration()
    {
        var listing = Listing();

        Assert.Equal(MarketListingState.Active, listing.State);
        Assert.Equal(Now.AddHours(48), listing.ExpiresAt);
        Assert.Equal(20, listing.PricePerUnit, 3);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, 0)]
    public void Constructor_ShouldRejectANonPositivePriceOrUnits(int price, double units)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Listing(price, units));

    [Fact]
    public void Buy_ShouldCloseTheListingAsSold()
    {
        var listing = Listing();
        var buyer = Guid.NewGuid();

        listing.Buy(buyer, Now.AddHours(1));

        Assert.Equal(MarketListingState.Sold, listing.State);
        Assert.Equal(buyer, listing.BuyerId);
        Assert.Equal(Now.AddHours(1), listing.ClosedAt);
    }

    /// <summary>Другий покупець бачить закритий лот, а не купує той самий предмет удруге.</summary>
    [Fact]
    public void Buy_ShouldRefuse_WhenTheListingIsAlreadySold()
    {
        var listing = Listing();
        listing.Buy(Guid.NewGuid(), Now);

        var refusal = Assert.Throws<InvalidStateException>(() => listing.Buy(Guid.NewGuid(), Now));

        Assert.Equal(RefusalReasons.MarketListingClosed.Key, refusal.Reason);
    }

    /// <summary>Строк минув, а сканер ще не дійшов — купити однаково не можна.</summary>
    [Fact]
    public void Buy_ShouldRefuse_AfterTheListingExpires()
    {
        var listing = Listing();

        var refusal = Assert.Throws<InvalidStateException>(() => listing.Buy(Guid.NewGuid(), Now.AddHours(48)));

        Assert.Equal(RefusalReasons.MarketListingClosed.Key, refusal.Reason);
    }

    [Fact]
    public void Buy_ShouldRefuse_TheSellersOwnListing()
    {
        var refusal = Assert.Throws<RequirementNotMetException>(() => Listing().Buy(Seller, Now));

        Assert.Equal(RefusalReasons.MarketOwnListing.Key, refusal.Reason);
    }

    [Fact]
    public void Cancel_ShouldCloseTheListing()
    {
        var listing = Listing();

        listing.Cancel(Now);

        Assert.Equal(MarketListingState.Cancelled, listing.State);
    }

    /// <summary>Продане вже не зняти: предмет у покупця.</summary>
    [Fact]
    public void Cancel_ShouldRefuse_ASoldListing()
    {
        var listing = Listing();
        listing.Buy(Guid.NewGuid(), Now);

        Assert.Throws<InvalidStateException>(() => listing.Cancel(Now));
    }

    [Fact]
    public void Expire_ShouldCloseTheListing_OnceItsTimeHasCome()
    {
        var listing = Listing();

        listing.Expire(Now.AddHours(48));

        Assert.Equal(MarketListingState.Expired, listing.State);
    }

    [Fact]
    public void Expire_ShouldRefuse_BeforeTheDeadline()
        => Assert.Throws<InvalidStateException>(() => Listing().Expire(Now.AddHours(47)));
}
