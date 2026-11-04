using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.Domain.Tests.Technologies;

/// <summary>Вітрина виводиться з періоду: однакова для всіх, без повторів, із ціною після знижки.</summary>
public class ClanShopTests
{
    private static readonly DateTime Wednesday = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static ClanShopItemConfig Item(string key, int weight = 1, int price = 1000, int maxDiscount = 90)
        => new() { ItemKey = key, Price = price, Limit = 2, Weight = weight, MaxDiscount = maxDiscount };

    private static ClanShop Shop(int slots, List<ClanShopDiscountConfig>? discounts = null, params ClanShopItemConfig[] pool)
        => new(new ClanShopConfig
        {
            DailySlots = slots,
            WeeklySlots = slots,
            DailyPool = pool.ToList(),
            WeeklyPool = pool.ToList(),
            DailyDiscounts = discounts ?? [],
            WeeklyDiscounts = discounts ?? []
        });

    private static readonly ClanShopItemConfig[] Pool = Enumerable.Range(1, 20).Select(i => Item($"item{i}")).ToArray();

    [Theory]
    [InlineData(2026, 11, 2, "weekly:2026-11-02")]
    [InlineData(2026, 11, 4, "weekly:2026-11-02")]
    [InlineData(2026, 11, 8, "weekly:2026-11-02")]
    [InlineData(2026, 11, 9, "weekly:2026-11-09")]
    public void WeeklyPeriod_ShouldStartOnMonday(int year, int month, int day, string period)
        => Assert.Equal(period, ClanShop.Period(ClanShopKind.Weekly, new DateTime(year, month, day, 23, 0, 0, DateTimeKind.Utc)));

    [Fact]
    public void DailyPeriod_ShouldChangeAtUtcMidnight()
    {
        Assert.Equal("daily:2026-11-04", ClanShop.Period(ClanShopKind.Daily, Wednesday));
        Assert.Equal("daily:2026-11-05", ClanShop.Period(ClanShopKind.Daily, Wednesday.Date.AddDays(1)));
    }

    /// <summary>Два окремі екземпляри — як два процеси сервера: вітрина мусить збігтись.</summary>
    [Fact]
    public void Offers_ShouldBeTheSameForEveryone_WithinAPeriod()
    {
        var morning = Shop(15, null, Pool).Offers(ClanShopKind.Daily, Wednesday.Date);
        var evening = Shop(15, null, Pool).Offers(ClanShopKind.Daily, Wednesday.Date.AddHours(23));

        Assert.Equal(morning, evening);
    }

    [Fact]
    public void Offers_ShouldChange_InTheNextPeriod()
    {
        var shop = Shop(15, null, Pool);

        Assert.NotEqual(
            shop.Offers(ClanShopKind.Daily, Wednesday).Select(o => o.ItemKey),
            shop.Offers(ClanShopKind.Daily, Wednesday.AddDays(1)).Select(o => o.ItemKey));
    }

    [Fact]
    public void Offers_ShouldFillTheSlots_WithoutRepeats()
    {
        var offers = Shop(15, null, Pool).Offers(ClanShopKind.Weekly, Wednesday);

        Assert.Equal(15, offers.Count);
        Assert.Equal(15, offers.Select(o => o.ItemKey).Distinct().Count());
    }

    [Fact]
    public void Offers_ShouldApplyTheRolledDiscount()
    {
        var offers = Shop(1, [new ClanShopDiscountConfig { Percent = 20, Weight = 1 }], Item("item", price: 1000))
            .Offers(ClanShopKind.Daily, Wednesday);

        var offer = Assert.Single(offers);
        Assert.Equal(20, offer.DiscountPercent);
        Assert.Equal(800, offer.Price);
        Assert.Equal(1000, offer.BasePrice);
    }

    /// <summary>Унікальні предмети обрізають знижку: −90% на них випало б «майже задарма».</summary>
    [Fact]
    public void Offers_ShouldClipTheDiscount_ToTheItemsMaximum()
    {
        var offers = Shop(1, [new ClanShopDiscountConfig { Percent = 90, Weight = 1 }], Item("unique", price: 1000, maxDiscount: 50))
            .Offers(ClanShopKind.Weekly, Wednesday);

        var offer = Assert.Single(offers);
        Assert.Equal(50, offer.DiscountPercent);
        Assert.Equal(500, offer.Price);
    }

    [Fact]
    public void Offers_ShouldFavourHeavierItems()
    {
        var heavy = Item("heavy", weight: 1000);
        var light = Item("light", weight: 1);
        var shop = Shop(1, null, heavy, light);

        var days = Enumerable.Range(0, 200).Select(d => shop.Offers(ClanShopKind.Daily, Wednesday.AddDays(d))[0].ItemKey).ToList();

        Assert.True(days.Count(k => k == "heavy") > 190);
    }
}
