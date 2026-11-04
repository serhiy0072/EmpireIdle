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
using EmpireIdle.Domain.ValueObjects;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Підношення: що списують і що дають. Кидок 0,5 при вагах 90/10 — завжди ×1, тобто 100 монет.</summary>
public class ClanOfferingCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _research = Substitute.For<IClanResearchRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IPlayerWalletRepository _wallets = Substitute.For<IPlayerWalletRepository>();
    private readonly ICurrentPlayer _currentPlayer = Substitute.For<ICurrentPlayer>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly IRewardGranter _granter = Substitute.For<IRewardGranter>();
    private readonly Clan _clan;
    private readonly ClanResearch _target;
    private readonly ClanOfferingLedger _ledger;
    private readonly ClanTechnologies _technologies;
    private readonly GameCatalog _catalog;
    private readonly Village _village;

    public ClanOfferingCommandTests()
    {
        var config = new GameConfigBuilder()
            .WithTechnologies(t => t.Clan.DailyContributionRewards =
            [
                new ClanDailyRewardConfig { Points = 20, Rewards = [new RewardConfig { Type = "Gems", Amount = 5 }] },
                new ClanDailyRewardConfig { Points = 60, Rewards = [new RewardConfig { Type = "Gems", Amount = 7 }] }
            ])
            .Build();
        _catalog = new GameCatalog(config);
        _technologies = new ClanTechnologies(config.Technologies.Clan);

        _clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", PlayerId, Now);
        _clans.GetByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_clan);

        _target = new ClanResearch(Guid.NewGuid(), _clan.Id, 1, Now);
        _research.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns(_target);

        _ledger = new ClanOfferingLedger(Guid.NewGuid(), PlayerId, 1, Now);
        _offerings.GetLedgerAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_ledger);

        _village = Entities.VillageWithTownhall(townhallLevel: 10);
        _villages.GetByPlayerIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(_village);

        _random.NextDouble().Returns(0.5);
        _granter.RewardType.Returns("Gems");
        _serverContext.ServerId.Returns(1);
        _currentPlayer.UserId.Returns(UserId);
    }

    private ClanOfferings Offerings()
        => new(_clans, _research, _offerings, _serverContext, _technologies, _random, new RewardDispatcher([_granter]));

    private OfferResourcesCommandHandler ResourcesHandler()
        => new(Offerings(), _villages, _unitOfWork, _technologies, new VillageStatus(_catalog),
            new FakeTimeProvider(Now), NullLogger<OfferResourcesCommandHandler>.Instance);

    private OfferGemsCommandHandler GemsHandler()
        => new(Offerings(), _wallets, _currentPlayer, _unitOfWork, _technologies,
            new FakeTimeProvider(Now), NullLogger<OfferGemsCommandHandler>.Instance);

    private PlayerWallet GivenWallet(int gems)
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "test", Now);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        return wallet;
    }

    private long Resource(string key) => _village.Resources.Single(r => r.ResourceType == key).Amount;

    [Fact]
    public async Task OfferResources_ShouldChargeByTownHall_AndGivePointsContributionAndCoins()
    {
        ClanContribution? contribution = null;
        await _offerings.AddContributionAsync(Arg.Do<ClanContribution>(c => contribution = c), Arg.Any<CancellationToken>());
        var goldBefore = Resource(TestKeys.Gold);

        var result = await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        // Ратуша 10 × 50 золота за спробу
        Assert.Equal(goldBefore - 500, Resource(TestKeys.Gold));
        Assert.Equal(10, _target.UnallocatedPoints);
        Assert.Equal(10, result.ClanPoints);
        Assert.Equal(100, result.Coins);
        Assert.Equal(100, _ledger.Coins);
        Assert.Equal(24, _ledger.AttemptsAvailable(25, TimeSpan.FromSeconds(576), Now));
        Assert.Equal(10, contribution!.Total);
        Assert.Equal(10, contribution.TodayAt(Now));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OfferResources_ShouldRefuse_AResourceWithoutAPrice()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Iron), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanTechResourceRejected.Key, error.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Підношення не залежить від офіцера: перше з них саме створює запас клану.</summary>
    [Fact]
    public async Task OfferResources_ShouldWorkAndReward_InAClanThatNeverAllocatedAnything()
    {
        ClanResearch? created = null;
        _research.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns((ClanResearch?)null);
        await _research.AddAsync(Arg.Do<ClanResearch>(r => created = r), Arg.Any<CancellationToken>());

        var result = await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        Assert.Equal(100, result.Coins);
        Assert.Equal(10, created!.UnallocatedPoints);
    }

    [Fact]
    public async Task OfferResources_ShouldRefuse_WhenNoAttemptsAreLeft()
    {
        for (var i = 0; i < 25; i++)
            _ledger.SpendAttempt(25, TimeSpan.FromSeconds(576), Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanTechNoAttempts.Key, error.Reason);
    }

    /// <summary>Вихід і повернення не дають другої шкали: книга та сама, бо вона живе за гравцем.</summary>
    [Fact]
    public async Task OfferResources_ShouldReuseThePlayersLedger_InAnotherClan()
    {
        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        var other = new Clan(Guid.NewGuid(), 1, "Beta", "BET", PlayerId, Now);
        _clans.GetByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(other);
        var otherTarget = new ClanResearch(Guid.NewGuid(), other.Id, 1, Now);
        _research.GetByClanAsync(other.Id, Arg.Any<CancellationToken>()).Returns(otherTarget);

        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        Assert.Equal(23, _ledger.AttemptsAvailable(25, TimeSpan.FromSeconds(576), Now));
        Assert.Equal(200, _ledger.Coins);
        Assert.Equal(20, _ledger.PointsToday);
        await _offerings.DidNotReceive().AddLedgerAsync(Arg.Any<ClanOfferingLedger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OfferGems_ShouldChargeTheBatchPrice_AndGiveFiftyPoints()
    {
        var wallet = GivenWallet(100);

        await GemsHandler().Handle(new OfferGemsCommand(PlayerId, 10), CancellationToken.None);

        Assert.Equal(90, wallet.GemBalance.Value);
        Assert.Equal(50, _target.UnallocatedPoints);
        Assert.Equal(2, _ledger.NextGemOffering(Now));
        Assert.Equal(25, _ledger.AttemptsAvailable(25, TimeSpan.FromSeconds(576), Now));
    }

    [Fact]
    public async Task OfferGems_ShouldRefuseAndChargeNothing_WhenThePriceMovedOn()
    {
        var wallet = GivenWallet(1000);

        for (var i = 0; i < 5; i++)
            _ledger.RecordGemOffering(Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => GemsHandler().Handle(new OfferGemsCommand(PlayerId, 10), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanTechGemPriceChanged.Key, error.Reason);
        Assert.Equal(20, Assert.IsType<int>(error.Args["price"]));
        Assert.Equal(1000, wallet.GemBalance.Value);
    }

    [Fact]
    public async Task OfferGems_ShouldRefuse_AfterTheDailyLimit()
    {
        GivenWallet(100_000);

        for (var i = 0; i < 50; i++)
            _ledger.RecordGemOffering(Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => GemsHandler().Handle(new OfferGemsCommand(PlayerId, 10240), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanTechGemLimit.Key, error.Reason);
    }

    /// <summary>Поріг 20 перетинає друга спроба, і лише вона; поріг 60 ще попереду.</summary>
    [Fact]
    public async Task Offering_ShouldGrantADailyReward_OnlyWhenCrossingItsThreshold()
    {
        var first = await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);
        var second = await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);
        var third = await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        Assert.Empty(first.DailyRewardsReached);
        Assert.Equal(20, Assert.Single(second.DailyRewardsReached));
        Assert.Empty(third.DailyRewardsReached);

        await _granter.Received(1).GrantAsync(Arg.Is<RewardContext>(c => c.PlayerId == PlayerId && c.Reward.Amount == 5),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Велике gems-підношення може перетнути кілька порогів разом: кожен дає свою нагороду.</summary>
    [Fact]
    public async Task Offering_ShouldGrantEveryThresholdItCrosses()
    {
        GivenWallet(100);
        _ledger.AddPoints(15, Now);

        var result = await GemsHandler().Handle(new OfferGemsCommand(PlayerId, 10), CancellationToken.None);

        Assert.Equal([20, 60], result.DailyRewardsReached);
        await _granter.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Перехід в інший клан не дає порога вдруге: сума дня живе в особистій книзі.</summary>
    [Fact]
    public async Task Offering_ShouldNotRepeatTheThreshold_InAnotherClan()
    {
        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);
        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        var other = new Clan(Guid.NewGuid(), 1, "Beta", "BET", PlayerId, Now);
        _clans.GetByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(other);
        var otherTarget = new ClanResearch(Guid.NewGuid(), other.Id, 1, Now);
        _research.GetByClanAsync(other.Id, Arg.Any<CancellationToken>()).Returns(otherTarget);

        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);
        await ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, TestKeys.Gold), CancellationToken.None);

        await _granter.Received(1).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }
}
