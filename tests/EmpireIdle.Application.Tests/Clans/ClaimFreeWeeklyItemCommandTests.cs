using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Rewards.Contracts;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Безкоштовна вітрина: предмет усім, клановий телепорт — лише в клані; ліміт на гравця за тиждень.</summary>
public class ClaimFreeWeeklyItemCommandTests
{
    private const string Open = "open_item";
    private const string ForClan = "clan_item";

    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _research = Substitute.For<IClanResearchRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly ClanOfferingLedger _ledger = new(Guid.NewGuid(), PlayerId, 1, Now);
    private readonly ClanTechnologies _technologies;
    private readonly ClanShop _shop;

    public ClaimFreeWeeklyItemCommandTests()
    {
        var config = new GameConfigBuilder().WithTechnologies(t => t.Clan.Shop.FreeWeekly =
        [
            new ClanFreeItemConfig { ItemKey = Open, Limit = 3 },
            new ClanFreeItemConfig { ItemKey = ForClan, Limit = 2, RequiresClan = true }
        ]).Build();
        _technologies = new ClanTechnologies(config.Technologies.Clan);
        _shop = new ClanShop(config.Technologies.Clan.Shop);

        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _offerings.GetLedgerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ledger);
        _granter.RewardType.Returns("Item");
        _serverContext.ServerId.Returns(1);
    }

    private Task Claim(string item = Open, int quantity = 1, DateTime? at = null)
    {
        var dispatcher = new RewardDispatcher([_granter]);
        var offerings = new ClanOfferings(_clans, _research, _offerings, _serverContext, _technologies, _random, dispatcher);

        return new ClaimFreeWeeklyItemCommandHandler(_clans, offerings, _unitOfWork, _shop, dispatcher,
                new FakeTimeProvider(at ?? Now), NullLogger<ClaimFreeWeeklyItemCommandHandler>.Instance)
            .Handle(new ClaimFreeWeeklyItemCommand(PlayerId, item, quantity), CancellationToken.None);
    }

    [Fact]
    public async Task Claim_ShouldGiveTheItemWithoutAClan_AndCostNothing()
    {
        _ledger.AddCoins(500);

        await Claim(quantity: 2);

        Assert.Equal(500, _ledger.Coins);
        Assert.Equal(2, _ledger.PurchasedIn(ClanShop.FreePeriod(Now), Open));
        await _granter.Received(1).GrantAsync(
            Arg.Is<RewardContext>(c => c.PlayerId == PlayerId && c.Reward.Key == Open && c.Reward.Amount == 2),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Гравець без клану й без жодного підношення: книги ще немає, її створює вітрина.</summary>
    [Fact]
    public async Task Claim_ShouldCreateTheLedger_ForAPlayerWhoNeverOffered()
    {
        _offerings.GetLedgerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns((ClanOfferingLedger?)null);

        await Claim();

        await _offerings.Received(1).AddLedgerAsync(
            Arg.Is<ClanOfferingLedger>(l => l.PlayerId == PlayerId && l.PurchasedIn(ClanShop.FreePeriod(Now), Open) == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Claim_ShouldRefuse_TheClanItemWithoutAClan()
    {
        var error = await Assert.ThrowsAsync<InvalidStateException>(() => Claim(ForClan));

        Assert.Equal(RefusalReasons.ClanNotMember.Key, error.Reason);
        Assert.Equal(0, _ledger.PurchasedIn(ClanShop.FreePeriod(Now), ForClan));
        await _granter.DidNotReceive().GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Claim_ShouldGiveTheClanItem_ToAClanMember()
    {
        _clans.GetClanIdByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(Guid.NewGuid());

        await Claim(ForClan);

        Assert.Equal(1, _ledger.PurchasedIn(ClanShop.FreePeriod(Now), ForClan));
    }

    [Fact]
    public async Task Claim_ShouldRefuse_OverTheWeeklyLimit_AcrossClaims()
    {
        await Claim(quantity: 2);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Claim(quantity: 2));

        Assert.Equal(RefusalReasons.ClanTechShopLimit.Key, error.Reason);
        Assert.Equal(2, _ledger.PurchasedIn(ClanShop.FreePeriod(Now), Open));
    }

    [Fact]
    public async Task Claim_ShouldOpenTheLimitAnew_NextWeek_ButNotTheNextDay()
    {
        await Claim(quantity: 3);

        await Assert.ThrowsAsync<RequirementNotMetException>(() => Claim(at: Now.AddDays(1)));
        await Claim(quantity: 3, at: Now.AddDays(7));

        Assert.Equal(3, _ledger.PurchasedIn(ClanShop.FreePeriod(Now.AddDays(7)), Open));
    }

    /// <summary>Той самий предмет за монети має власний ліміт: безкоштовний його не з'їдає.</summary>
    [Fact]
    public async Task Claim_ShouldNotSpendTheCoinShopLimit_OfTheSameItem()
    {
        await Claim(quantity: 3);

        Assert.Equal(0, _ledger.PurchasedIn(ClanShop.Period(ClanShopKind.Weekly, Now), Open));
    }

    [Fact]
    public async Task Claim_ShouldRefuse_AnItemThatIsNotOnTheShowcase()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Claim("nothing"));

        Assert.Equal(RefusalReasons.ClanTechShopItemGone.Key, error.Reason);
    }
}
