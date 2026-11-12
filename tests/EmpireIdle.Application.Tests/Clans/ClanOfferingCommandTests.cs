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

/// <summary>
/// Підношення у вибраний вузол: що списують і що дають. Кидок 0,5 при вагах 90/10 — завжди ×1, тобто 100 монет.
/// Тестовий конфіг: v_attack_all — 100 золота за спробу на тірі 1, c_coins — 50 дерева й відкривається після нього.
/// </summary>
public class ClanOfferingCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid PlayerId = Guid.NewGuid();
    private const string UserId = "user-1";
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");
    private static readonly TechnologyKey Mint = new(1, "c_coins");
    private static readonly TimeSpan Recharge = TimeSpan.FromSeconds(576);

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
        => new(_clans, _research, _offerings, _serverContext, _technologies, _random,
            new RewardDispatcher([_granter]), TestTechnologies.Settlement(_catalog, _villages));

    private OfferResourcesCommandHandler ResourcesHandler()
        => new(Offerings(), _villages, _unitOfWork, _technologies, new FakeTimeProvider(Now),
            NullLogger<OfferResourcesCommandHandler>.Instance);

    private OfferGemsCommandHandler GemsHandler()
        => new(Offerings(), _wallets, _currentPlayer, _unitOfWork, _technologies,
            new FakeTimeProvider(Now), NullLogger<OfferGemsCommandHandler>.Instance);

    private Task<ClanOfferingResult> OfferResources(TechnologyKey key)
        => ResourcesHandler().Handle(new OfferResourcesCommand(PlayerId, key.Tier, key.Node), CancellationToken.None);

    private Task<ClanOfferingResult> OfferGems(TechnologyKey key, int expectedPrice)
        => GemsHandler().Handle(new OfferGemsCommand(PlayerId, key.Tier, key.Node, expectedPrice), CancellationToken.None);

    private PlayerWallet GivenWallet(int gems)
    {
        var wallet = new PlayerWallet(Guid.NewGuid(), UserId);
        wallet.AddGems(new GemAmount(gems), "test", Now);
        _wallets.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(wallet);

        return wallet;
    }

    private void GivenStandardLevel() => _target.Invest(Standard, _technologies.Points(1, _technologies.Tree.Node(Standard.Node)!, 1), _technologies, Now);

    private long Resource(string key) => _village.Resources.Single(r => r.ResourceType == key).Amount;

    [Fact]
    public async Task OfferResources_ShouldChargeTheNodesPrice_AndGivePointsContributionAndCoins()
    {
        ClanContribution? contribution = null;
        await _offerings.AddContributionAsync(Arg.Do<ClanContribution>(c => contribution = c), Arg.Any<CancellationToken>());
        var goldBefore = Resource(TestKeys.Gold);

        var result = await OfferResources(Standard);

        // Ціну задає вузол і тір, а не ратуша 10
        Assert.Equal(goldBefore - 100, Resource(TestKeys.Gold));
        Assert.Equal(10, _target.ProgressOf(Standard));
        Assert.Equal(10, result.Progress);
        Assert.Equal(0, result.Level);
        Assert.Empty(result.LevelsCompleted);
        Assert.Equal(100, result.Coins);
        Assert.Equal(100, _ledger.Coins);
        Assert.Equal(24, _ledger.AttemptsAvailable(25, Recharge, Now));
        Assert.Equal(10, contribution!.Total);
        Assert.Equal(10, contribution.TodayAt(Now));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>У кожного вузла свій ресурс: c_coins бере дерево за своїм курсом.</summary>
    [Fact]
    public async Task OfferResources_ShouldTakeTheResourceOfTheChosenNode()
    {
        GivenStandardLevel();
        var goldBefore = Resource(TestKeys.Gold);
        var woodBefore = Resource(TestKeys.Wood);

        await OfferResources(Mint);

        Assert.Equal(woodBefore - 50, Resource(TestKeys.Wood));
        Assert.Equal(goldBefore, Resource(TestKeys.Gold));
        Assert.Equal(10, _target.ProgressOf(Mint));
    }

    [Fact]
    public async Task OfferResources_ShouldReportTheLevelItCloses()
    {
        var points = _technologies.Points(1, _technologies.Tree.Node(Standard.Node)!, 1);
        _target.Invest(Standard, points - 10, _technologies, Now);

        var result = await OfferResources(Standard);

        Assert.Equal(1, result.Level);
        Assert.Equal([1], result.LevelsCompleted);
        Assert.Equal(0, result.Progress);
    }

    /// <summary>Закритий вузол нічого б не дав: відмова до спроби й списання.</summary>
    [Fact]
    public async Task OfferResources_ShouldRefuseWithoutCharging_ANodeWithMissingPrerequisites()
    {
        var woodBefore = Resource(TestKeys.Wood);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferResources(Mint));

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, error.Reason);
        Assert.Equal(woodBefore, Resource(TestKeys.Wood));
        Assert.Equal(25, _ledger.AttemptsAvailable(25, Recharge, Now));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OfferResources_ShouldRefuse_AMaxedNode()
    {
        _target.Invest(Standard, 1_000_000, _technologies, Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferResources(Standard));

        Assert.Equal(RefusalReasons.ResearchMaxed.Key, error.Reason);
    }

    /// <summary>Наступний тір відкриває не рівень світу, а рівень кожного вузла останнього шару попереднього тіру.</summary>
    [Fact]
    public async Task OfferResources_ShouldOpenTheNextTier_OnceTheLastLayerHasALevel()
    {
        var nextTier = new TechnologyKey(2, Standard.Node);
        GivenStandardLevel();

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferResources(nextTier));
        Assert.Equal(RefusalReasons.ResearchPreviousTier.Key, error.Reason);

        _target.Invest(Mint, _technologies.Points(1, _technologies.Tree.Node(Mint.Node)!, 1), _technologies, Now);
        var goldBefore = Resource(TestKeys.Gold);

        await OfferResources(nextTier);

        Assert.Equal(goldBefore - 200, Resource(TestKeys.Gold));
        Assert.Equal(10, _target.ProgressOf(nextTier));
    }

    [Fact]
    public async Task OfferResources_ShouldNotFind_AnUnknownTechnology()
        => await Assert.ThrowsAsync<EntityNotFoundException>(() => OfferResources(new TechnologyKey(1, "nope")));

    /// <summary>Перше підношення клану саме створює його технології.</summary>
    [Fact]
    public async Task OfferResources_ShouldCreateTheClansResearch_OnTheFirstOffering()
    {
        ClanResearch? created = null;
        _research.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns((ClanResearch?)null);
        await _research.AddAsync(Arg.Do<ClanResearch>(r => created = r), Arg.Any<CancellationToken>());

        var result = await OfferResources(Standard);

        Assert.Equal(100, result.Coins);
        Assert.Equal(10, created!.ProgressOf(Standard));
    }

    [Fact]
    public async Task OfferResources_ShouldRefuse_WhenNoAttemptsAreLeft()
    {
        for (var i = 0; i < 25; i++)
            _ledger.SpendAttempt(25, Recharge, Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferResources(Standard));

        Assert.Equal(RefusalReasons.ClanTechNoAttempts.Key, error.Reason);
    }

    /// <summary>Вихід і повернення не дають другої шкали: книга та сама, бо вона живе за гравцем.</summary>
    [Fact]
    public async Task OfferResources_ShouldReuseThePlayersLedger_InAnotherClan()
    {
        await OfferResources(Standard);

        GivenAnotherClan();

        await OfferResources(Standard);

        Assert.Equal(23, _ledger.AttemptsAvailable(25, Recharge, Now));
        Assert.Equal(200, _ledger.Coins);
        Assert.Equal(20, _ledger.PointsToday);
        await _offerings.DidNotReceive().AddLedgerAsync(Arg.Any<ClanOfferingLedger>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OfferGems_ShouldChargeTheBatchPrice_AndInvestFiftyPointsIntoTheChosenNode()
    {
        var wallet = GivenWallet(100);
        GivenStandardLevel();

        await OfferGems(Mint, 10);

        Assert.Equal(90, wallet.GemBalance.Value);
        Assert.Equal(50, _target.ProgressOf(Mint));
        Assert.Equal(2, _ledger.NextGemOffering(Now));
        Assert.Equal(25, _ledger.AttemptsAvailable(25, Recharge, Now));
    }

    [Fact]
    public async Task OfferGems_ShouldRefuseWithoutCharging_AClosedNode()
    {
        var wallet = GivenWallet(100);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferGems(Mint, 10));

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, error.Reason);
        Assert.Equal(100, wallet.GemBalance.Value);
        Assert.Equal(1, _ledger.NextGemOffering(Now));
    }

    [Fact]
    public async Task OfferGems_ShouldRefuseAndChargeNothing_WhenThePriceMovedOn()
    {
        var wallet = GivenWallet(1000);

        for (var i = 0; i < 5; i++)
            _ledger.RecordGemOffering(Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferGems(Standard, 10));

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

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => OfferGems(Standard, 10240));

        Assert.Equal(RefusalReasons.ClanTechGemLimit.Key, error.Reason);
    }

    /// <summary>Поріг 20 перетинає друга спроба, і лише вона; поріг 60 ще попереду.</summary>
    [Fact]
    public async Task Offering_ShouldGrantADailyReward_OnlyWhenCrossingItsThreshold()
    {
        var first = await OfferResources(Standard);
        var second = await OfferResources(Standard);
        var third = await OfferResources(Standard);

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

        var result = await OfferGems(Standard, 10);

        Assert.Equal([20, 60], result.DailyRewardsReached);
        await _granter.Received(2).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Перехід в інший клан не дає порога вдруге: сума дня живе в особистій книзі.</summary>
    [Fact]
    public async Task Offering_ShouldNotRepeatTheThreshold_InAnotherClan()
    {
        await OfferResources(Standard);
        await OfferResources(Standard);

        GivenAnotherClan();

        await OfferResources(Standard);
        await OfferResources(Standard);

        await _granter.Received(1).GrantAsync(Arg.Any<RewardContext>(), Arg.Any<CancellationToken>());
    }

    private void GivenAnotherClan()
    {
        var other = new Clan(Guid.NewGuid(), 1, "Beta", "BET", PlayerId, Now);
        _clans.GetByMemberAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(other);
        var otherTarget = new ClanResearch(Guid.NewGuid(), other.Id, 1, Now);
        _research.GetByClanAsync(other.Id, Arg.Any<CancellationToken>()).Returns(otherTarget);
    }
}
