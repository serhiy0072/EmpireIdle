using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Магазин фікстури: один предмет по 1000 монет, ліміт 3 на добу; знижок немає.</summary>
public class BuyClanShopItemCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly ClanOfferingLedger _ledger = new(Guid.NewGuid(), PlayerId, 1, Now);
    private readonly ClanShop _shop = new(new GameConfigBuilder().WithTechnologies().Build().Technologies.Clan.Shop);

    public BuyClanShopItemCommandTests()
    {
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());
        _offerings.GetLedgerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ledger);
        _granter.RewardType.Returns("Item");
    }

    private Task Buy(int quantity = 1, int price = 1000, ClanShopKind kind = ClanShopKind.Daily, string item = TestKeys.ClanShopItem, DateTime? at = null)
        => new BuyClanShopItemCommandHandler(_clans, _offerings, _unitOfWork, _shop, new RewardDispatcher([_granter]),
                new FakeTimeProvider(at ?? Now), NullLogger<BuyClanShopItemCommandHandler>.Instance)
            .Handle(new BuyClanShopItemCommand(PlayerId, kind, item, quantity, price), CancellationToken.None);

    [Fact]
    public async Task Buy_ShouldTakeTheCoins_AndGrantTheItem()
    {
        _ledger.AddCoins(2500);

        await Buy(quantity: 2);

        Assert.Equal(500, _ledger.Coins);
        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == PlayerId && c.Reward.Key == TestKeys.ClanShopItem && c.Reward.Amount == 2),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Buy_ShouldRefuse_OutsideAClan_ThoughTheCoinsStay()
    {
        _ledger.AddCoins(5000);
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var error = await Assert.ThrowsAsync<InvalidStateException>(() => Buy());

        Assert.Equal(RefusalReasons.ClanNotMember.Key, error.Reason);
        Assert.Equal(5000, _ledger.Coins);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_WhenCoinsAreShort()
    {
        _ledger.AddCoins(999);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy());

        Assert.Equal(RefusalReasons.ClanTechNoCoins.Key, error.Reason);
        Assert.Equal(999, _ledger.Coins);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_WhenThePlayerNeverOffered()
    {
        _offerings.GetLedgerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((ClanOfferingLedger?)null);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy());

        Assert.Equal(RefusalReasons.ClanTechNoCoins.Key, error.Reason);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_OverTheLimitOfThePeriod_AcrossPurchases()
    {
        _ledger.AddCoins(100_000);

        await Buy(quantity: 2);
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(quantity: 2));

        Assert.Equal(RefusalReasons.ClanTechShopLimit.Key, error.Reason);
        Assert.Equal(98_000, _ledger.Coins);
    }

    [Fact]
    public async Task Buy_ShouldOpenTheLimitAnew_InTheNextPeriod()
    {
        _ledger.AddCoins(100_000);

        await Buy(quantity: 3);
        await Buy(quantity: 3, at: Now.AddDays(1));

        Assert.Equal(94_000, _ledger.Coins);
    }

    /// <summary>Денні й тижневі ліміти — різні, хоч предмет той самий.</summary>
    [Fact]
    public async Task Buy_ShouldCountDailyAndWeeklyLimitsApart()
    {
        _ledger.AddCoins(100_000);

        await Buy(quantity: 3);
        await Buy(quantity: 1, price: 5000, kind: ClanShopKind.Weekly);

        Assert.Equal(92_000, _ledger.Coins);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_WhenThePriceMovedOn()
    {
        _ledger.AddCoins(5000);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(price: 900));

        Assert.Equal(RefusalReasons.ClanTechShopPriceChanged.Key, error.Reason);
        Assert.Equal(5000, _ledger.Coins);
    }

    [Fact]
    public async Task Buy_ShouldRefuse_AnItemThatIsNotOnTheShowcase()
    {
        _ledger.AddCoins(5000);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Buy(item: "nothing"));

        Assert.Equal(RefusalReasons.ClanTechShopItemGone.Key, error.Reason);
    }
}
