using EmpireIdle.Application.Market.Commands;
using EmpireIdle.Application.Market.Queries;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Market;

/// <summary>Медіана з продажів і запити, які бачить гравець.</summary>
public class MarketPricesAndQueriesTests
{
    private readonly MarketTestBed _bed = new();

    private RecalculateMarketPricesCommandHandler Recalculate() => new(
        _bed.MarketRepository, _bed.Pricing, _bed.Catalog, _bed.UnitOfWork,
        new FakeTimeProvider(MarketTestBed.Now), NullLogger<RecalculateMarketPricesCommandHandler>.Instance);

    /// <summary>П'ять продажів — медіана є; нова категорія отримує свій знімок.</summary>
    [Fact]
    public async Task Recalculate_ShouldStoreTheMedianOfRecentSales()
    {
        _bed.MarketRepository.GetSalesSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([("weapon", 10.0), ("weapon", 11.0), ("weapon", 12.0), ("weapon", 13.0), ("weapon", 14.0)]);
        _bed.MarketRepository.GetSnapshotsAsync(Arg.Any<CancellationToken>()).Returns([]);

        MarketPriceSnapshot? added = null;
        await _bed.MarketRepository.AddSnapshotAsync(Arg.Do<MarketPriceSnapshot>(s => added = s), Arg.Any<CancellationToken>());

        await Recalculate().Handle(new RecalculateMarketPricesCommand(1), CancellationToken.None);

        Assert.NotNull(added);
        Assert.Equal("weapon", added.PricingKey);
        Assert.Equal(12, added.MedianPerUnit);
        Assert.Equal(5, added.Sales);
    }

    /// <summary>Категорія, де за вікно ніхто не продавав, повертається до якоря.</summary>
    [Fact]
    public async Task Recalculate_ShouldForgetAStaleMedian()
    {
        var stale = new MarketPriceSnapshot(1, "weapon", 20, 9, MarketTestBed.Now.AddDays(-5));

        _bed.MarketRepository.GetSalesSinceAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns([]);
        _bed.MarketRepository.GetSnapshotsAsync(Arg.Any<CancellationToken>()).Returns([stale]);

        await Recalculate().Handle(new RecalculateMarketPricesCommand(1), CancellationToken.None);

        Assert.Null(stale.MedianPerUnit);
        Assert.Equal(0, stale.Sales);
    }

    /// <summary>Котирування бачить той самий коридор, що й виставлення: меч Power 10 — 70–130.</summary>
    [Fact]
    public async Task Quote_ShouldMatchTheListingCorridor()
    {
        var sword = _bed.GivenSword();
        var handler = new GetMarketQuoteQueryHandler(_bed.Goods, _bed.Desk, _bed.Catalog);

        var quote = await handler.Handle(
            new GetMarketQuoteQuery(_bed.Seller, MarketListingKind.Equipment, sword.Id, null, null, 1), CancellationToken.None);

        Assert.Equal(70, quote.MinPrice);
        Assert.Equal(130, quote.MaxPrice);
        Assert.Equal(100, quote.SuggestedPrice);
    }

    [Fact]
    public async Task MyMarket_ShouldReportTheLimitAndOwnListings()
    {
        var listing = _bed.GivenListing(MarketListingKind.Item, itemKey: MarketTestBed.Tonic, quantity: 2, units: 2, price: 1000);
        _bed.MarketRepository.GetBySellerAsync(_bed.Seller, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([listing]);

        var handler = new GetMyMarketQueryHandler(_bed.MarketRepository, _bed.Villages, _bed.Desk, _bed.Projection, _bed.Catalog);

        var view = await handler.Handle(new GetMyMarketQuery(_bed.Seller), CancellationToken.None);

        Assert.True(view.IsOpen);
        Assert.Equal(2, view.ListingLimit);
        Assert.Equal(1, view.ActiveListings);
        Assert.True(Assert.Single(view.Listings).IsOwn);
    }

    /// <summary>Закритий ринок каже, з якої ратуші він відкриється, і не дає лотів.</summary>
    [Fact]
    public async Task MyMarket_ShouldBeClosed_UnderTheFog()
    {
        var bed = new MarketTestBed(townHallLevel: 1);
        bed.MarketRepository.GetBySellerAsync(bed.Seller, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        var view = await new GetMyMarketQueryHandler(bed.MarketRepository, bed.Villages, bed.Desk, bed.Projection, bed.Catalog)
            .Handle(new GetMyMarketQuery(bed.Seller), CancellationToken.None);

        Assert.False(view.IsOpen);
        Assert.Equal(MarketTestBed.MarketOpensAt, view.OpensAtTownHall);
        Assert.Equal(0, view.ListingLimit);
    }

    /// <summary>Вітрина додає до лота деталі екземпляра — стати меча.</summary>
    [Fact]
    public async Task Browse_ShouldAttachEquipmentDetails()
    {
        var sword = _bed.GivenSword();
        var listing = _bed.GivenListing(MarketListingKind.Equipment, equipmentId: sword.Id);

        _bed.MarketRepository.BrowseAsync(null, null, Arg.Any<DateTime>(), 0, 20, Arg.Any<CancellationToken>())
            .Returns(([listing], 1));
        _bed.Inventory.GetEquipmentByIdsReadOnlyAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([sword]);

        var page = await new GetMarketListingsQueryHandler(_bed.MarketRepository, _bed.Projection, new FakeTimeProvider(MarketTestBed.Now))
            .Handle(new GetMarketListingsQuery(_bed.Buyer, null, null, 1, 20), CancellationToken.None);

        var view = Assert.Single(page.Listings);
        Assert.False(view.IsOwn);
        Assert.Equal(10, view.Equipment!.Stats["Attack"]);
        Assert.Equal(1, page.Total);
    }
}
