using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Clans.ReadModels;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Кланові технології для екрана: дерево з поступом, стан підношень і денний рейтинг.</summary>
public class GetClanTechnologiesQueryTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _research = Substitute.For<IClanResearchRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly GameConfig _config = new GameConfigBuilder().WithTechnologies(t =>
    {
        t.Clan.BasePoints = 100;
        t.Clan.PointsRounding = 10;
        t.Clan.Offering.ResourceRotationEvery = 100;
        t.Clan.Offering.GemDailyLimit = 1;
        t.Clan.DailyContributionRewards =
        [
            new ClanDailyRewardConfig { Points = 10, Rewards = [new RewardConfig { Type = "Gems", Amount = 1 }] },
            new ClanDailyRewardConfig { Points = 50, Rewards = [new RewardConfig { Type = "Gems", Amount = 1 }] }
        ];
    }).Build();
    private readonly Village _village = Entities.VillageWithTownhall(townhallLevel: 6);

    public GetClanTechnologiesQueryTests()
    {
        _serverContext.ServerId.Returns(1);
        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
        _clans.GetClanIdByMemberAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(ClanId);
        _villages.GetByPlayerIdReadOnlyAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(_village);
        _offerings.GetMemberContributionsAsync(ClanId, Arg.Any<CancellationToken>()).Returns(new List<ClanContribution>());
    }

    private Task<ClanTechnologiesView?> Query(DateTime? at = null)
    {
        var catalog = new GameCatalog(_config);

        return new GetClanTechnologiesQueryHandler(_clans, _research, _offerings, _players, _villages, _servers, _serverContext,
                new ClanTechnologies(_config.Technologies.Clan), new VillageStatus(catalog), catalog, new FakeTimeProvider(at ?? Now))
            .Handle(new GetClanTechnologiesQuery(_village.PlayerId), CancellationToken.None);
    }

    private ClanResearch GivenResearch()
    {
        var research = new ClanResearch(Guid.NewGuid(), ClanId, 1, Now);
        _research.GetByClanReadOnlyAsync(ClanId, Arg.Any<CancellationToken>()).Returns(research);

        return research;
    }

    private ClanOfferingLedger GivenLedger()
    {
        var ledger = new ClanOfferingLedger(Guid.NewGuid(), _village.PlayerId, 1, Now);
        _offerings.GetLedgerReadOnlyAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns(ledger);

        return ledger;
    }

    [Fact]
    public async Task Handle_ShouldReturnNull_WithoutAClan()
    {
        _clans.GetClanIdByMemberAsync(_village.PlayerId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        Assert.Null(await Query());
    }

    /// <summary>Клан ще нічого не підносив: екран показує повну шкалу й перший ресурс черги, а не падає.</summary>
    [Fact]
    public async Task Handle_ShouldShowAFreshState_BeforeTheFirstOffering()
    {
        var view = await Query();

        Assert.NotNull(view);
        Assert.Equal(4, view.Nodes.Count);
        Assert.Equal([true, false], view.Tiers.Select(t => t.Open));
        Assert.Equal((0L, 0L), (view.UnallocatedPoints, view.Coins));

        var offering = view.Offering;
        Assert.Equal((TestKeys.Gold, 300), (offering.Resource, offering.ResourceCost));
        Assert.Equal(100, offering.OfferingsUntilRotation);
        Assert.Equal(offering.MaxAttempts, offering.Attempts);
        Assert.Null(offering.NextAttemptAt);
        Assert.Equal(10, offering.GemPrice);
    }

    [Fact]
    public async Task Handle_ShouldShowProgress_AndTheCostOfTheNextLevel()
    {
        var research = GivenResearch();
        research.AddPoints(160, Now);
        research.Allocate(new TechnologyKey(1, "v_attack_all"), 150, new ClanTechnologies(_config.Technologies.Clan), Now);

        var view = await Query();

        var node = view!.Nodes.Single(n => n.Tier == 1 && n.Node == "v_attack_all");
        Assert.Equal((1, 50, 130, "Available"), (node.Level, node.Progress, node.NextPoints, node.Availability));
        Assert.Equal(10, view.UnallocatedPoints);
        Assert.Equal("Available", view.Nodes.Single(n => n.Tier == 1 && n.Node == "c_coins").Availability);
        Assert.Contains(view.Bonuses, b => b.Stat == TechnologyStat.Attack && b.Target == null);
    }

    [Fact]
    public async Task Handle_ShouldHaveNoNextPoints_OnAMaxedNode()
    {
        var research = GivenResearch();
        research.AddPoints(100_000, Now);
        research.Allocate(new TechnologyKey(1, "v_attack_all"), 100_000, new ClanTechnologies(_config.Technologies.Clan), Now);

        var node = (await Query())!.Nodes.Single(n => n.Tier == 1 && n.Node == "v_attack_all");

        Assert.Equal(("Maxed", 3), (node.Availability, node.Level));
        Assert.Null(node.NextPoints);
    }

    /// <summary>Після сотні підношень клан приймає наступний ресурс, а лічильник іде вже до наступної зміни.</summary>
    [Fact]
    public async Task Handle_ShouldShowTheNextResource_AfterARotation()
    {
        var research = GivenResearch();

        for (var i = 0; i < 101; i++)
            research.CountResourceOffering();

        var offering = (await Query())!.Offering;

        Assert.Equal((TestKeys.Wood, 240), (offering.Resource, offering.ResourceCost));
        Assert.Equal(99, offering.OfferingsUntilRotation);
    }

    [Fact]
    public async Task Handle_ShouldShowSpentAttempts_AndTheNextRecharge()
    {
        var ledger = GivenLedger();
        var recharge = TimeSpan.FromSeconds(_config.Technologies.Clan.Offering.AttemptRechargeSeconds);
        ledger.SpendAttempt(25, recharge, Now);
        ledger.SpendAttempt(25, recharge, Now);

        var offering = (await Query(Now.AddSeconds(1)))!.Offering;

        Assert.Equal(23, offering.Attempts);
        Assert.Equal(Now + recharge, offering.NextAttemptAt);

        // Одна спроба вже відновилась: наступна — через ще один інтервал, а не від цього моменту
        var later = (await Query(Now + recharge + TimeSpan.FromSeconds(1)))!.Offering;

        Assert.Equal(24, later.Attempts);
        Assert.Equal(Now + recharge * 2, later.NextAttemptAt);
    }

    [Fact]
    public async Task Handle_ShouldHaveNoGemPrice_AfterTheDailyLimit()
    {
        var ledger = GivenLedger();
        ledger.RecordGemOffering(Now);

        var offering = (await Query())!.Offering;

        Assert.Null(offering.GemPrice);
        Assert.Equal((1, 1), (offering.GemOfferingsToday, offering.GemDailyLimit));
    }

    [Fact]
    public async Task Handle_ShouldMarkReachedThresholds()
    {
        var ledger = GivenLedger();
        ledger.AddPoints(10, Now);

        var daily = (await Query())!.Daily;

        Assert.Equal(10, daily.PointsToday);
        Assert.Equal([(10, true), (50, false)], daily.Rewards.Select(r => (r.Points, r.Reached)));
    }

    /// <summary>Новий день обнуляє лічильники на екрані раніше, ніж перше підношення оновить рядок.</summary>
    [Fact]
    public async Task Handle_ShouldForgetYesterday_WithoutWritingAnything()
    {
        var ledger = GivenLedger();
        ledger.AddPoints(10, Now);
        ledger.RecordGemOffering(Now);

        var view = await Query(Now.AddDays(1));

        Assert.Equal(0, view!.Daily.PointsToday);
        Assert.Equal(10, view.Offering.GemPrice);
        Assert.Equal(1, ledger.GemOfferingsToday);
    }

    [Fact]
    public async Task Handle_ShouldRankContributorsByToday_ThenByTotal()
    {
        var quiet = new ClanContribution(Guid.NewGuid(), ClanId, Guid.NewGuid(), 1, Now.AddDays(-1));
        quiet.Add(900, Now.AddDays(-1));
        var veteran = new ClanContribution(Guid.NewGuid(), ClanId, Guid.NewGuid(), 1, Now);
        veteran.Add(10, Now.AddDays(-1));
        veteran.Add(10, Now);
        var active = new ClanContribution(Guid.NewGuid(), ClanId, Guid.NewGuid(), 1, Now);
        active.Add(50, Now);
        _offerings.GetMemberContributionsAsync(ClanId, Arg.Any<CancellationToken>())
            .Returns(new List<ClanContribution> { quiet, veteran, active });
        _players.GetNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [active.PlayerId] = "Active" });

        var contributors = (await Query())!.Contributors;

        Assert.Equal([active.PlayerId, veteran.PlayerId, quiet.PlayerId], contributors.Select(c => c.PlayerId));
        Assert.Equal(("Active", 50L, 50L), (contributors[0].Name, contributors[0].Today, contributors[0].Total));
        Assert.Equal(("—", 0L, 900L), (contributors[2].Name, contributors[2].Today, contributors[2].Total));
    }
}
