using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Магазин для екрана: вітрини з лімітами гравця й безкоштовна частина, яку видно й без клану.</summary>
public class GetClanShopQueryTests
{
    private const string Free = "free_item";

    private static readonly DateTime Wednesday = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly ClanShop _shop = new(new GameConfigBuilder().WithTechnologies(t => t.Clan.Shop.FreeWeekly =
        [new ClanFreeItemConfig { ItemKey = Free, Limit = 5, RequiresClan = true }]).Build().Technologies.Clan.Shop);

    private Task<ClanShopView> Query(DateTime? at = null)
        => new GetClanShopQueryHandler(_clans, _offerings, _shop, new FakeTimeProvider(at ?? Wednesday))
            .Handle(new GetClanShopQuery(PlayerId), CancellationToken.None);

    /// <summary>Книги ще немає: нуль монет і нуль куплено, а не помилка.</summary>
    [Fact]
    public async Task Handle_ShouldWork_WithoutAClanOrALedger()
    {
        var view = await Query();

        Assert.False(view.InClan);
        Assert.Equal(0, view.Coins);
        Assert.Equal((TestKeys.ClanShopItem, 0), (view.Daily.Offers[0].ItemKey, view.Daily.Offers[0].Purchased));
        Assert.Equal((Free, 5, 0, true), (view.Free.Items[0].ItemKey, view.Free.Items[0].Limit, view.Free.Items[0].Claimed, view.Free.Items[0].RequiresClan));
    }

    [Fact]
    public async Task Handle_ShouldShowCoinsAndLimits_PerPeriod()
    {
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        var ledger = new ClanOfferingLedger(Guid.NewGuid(), PlayerId, 1, Wednesday);
        ledger.AddCoins(5000);
        var live = ClanShop.LivePeriods(Wednesday);
        ledger.Purchase(ClanShop.Period(ClanShopKind.Daily, Wednesday), TestKeys.ClanShopItem, 2, 3, 2000, live);
        ledger.Purchase(ClanShop.FreePeriod(Wednesday), Free, 4, 5, 0, live);
        _offerings.GetLedgerReadOnlyAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(ledger);

        var view = await Query();

        Assert.True(view.InClan);
        Assert.Equal(3000, view.Coins);
        Assert.Equal(2, view.Daily.Offers[0].Purchased);
        Assert.Equal(0, view.Weekly.Offers[0].Purchased);
        Assert.Equal(4, view.Free.Items[0].Claimed);

        var nextDay = await Query(Wednesday.AddDays(1));
        Assert.Equal(0, nextDay.Daily.Offers[0].Purchased);
        Assert.Equal(4, nextDay.Free.Items[0].Claimed);

        var nextWeek = await Query(Wednesday.AddDays(5));
        Assert.Equal(0, nextWeek.Free.Items[0].Claimed);
    }

    [Fact]
    public async Task Handle_ShouldTellWhenEachShowcaseRefreshes()
    {
        var view = await Query();

        Assert.Equal(new DateTime(2026, 11, 5, 0, 0, 0, DateTimeKind.Utc), view.Daily.RefreshesAt);
        Assert.Equal(new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc), view.Weekly.RefreshesAt);
        Assert.Equal(view.Weekly.RefreshesAt, view.Free.RefreshesAt);
    }
}
