using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Queries;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Допомога клану з дослідженням (рішення 28.10.2026): своя частка й ліміт — 0,5% повної тривалості
/// за допомогу до 50 допомог, а не 2% до 20, як у будівництва.
/// </summary>
public class ResearchHelpTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid HelperId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanHelpRepository _help = Substitute.For<IClanHelpRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IPlayerResearchRepository _research = Substitute.For<IPlayerResearchRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Help_ShouldCutHalfAPercentOfTheResearch_UnderItsOwnCap()
    {
        var config = new GameConfigBuilder().WithTechnologies().Build();

        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", OwnerId, Now);
        clan.Join(HelperId, capacity: 50, Now);
        _clans.GetByMemberAsync(HelperId, Arg.Any<CancellationToken>()).Returns(clan);
        _villages.GetByPlayerIdAsync(OwnerId, Arg.Any<CancellationToken>()).Returns(Entities.VillageWithTownhall());

        var research = new PlayerResearch(Guid.NewGuid(), OwnerId, 1, Now);
        research.Start(new TechnologyKey(1, "attack_infantry"), TimeSpan.FromHours(10), Now);
        _research.GetByPlayerAsync(OwnerId, Arg.Any<CancellationToken>()).Returns(research);

        var request = new ClanHelpRequest(Guid.NewGuid(), 1, clan.Id, OwnerId, ClanHelpTarget.Research, research.Id,
            research.PlannedDuration, Now.AddHours(10), Now);
        _help.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        var handler = new GiveClanHelpCommandHandler(_clans, _help, _villages, _garrisons, _research, _unitOfWork,
            TestTechnologies.ClanBonuses(new GameCatalog(config)), new GameCatalog(config), new FakeTimeProvider(Now), NullLogger<GiveClanHelpCommandHandler>.Instance);

        await handler.Handle(new GiveClanHelpCommand(HelperId, request.Id), CancellationToken.None);

        // 10 годин × 0,5% = 3 хвилини
        Assert.Equal(TimeSpan.FromHours(10) - TimeSpan.FromMinutes(3), research.Remaining(Now));
        Assert.Equal(1, request.HelpCount);
    }

    /// <summary>Два рівні по +2 в.п. піднімають кап будівництва з 40% до 44%: 22 допомоги по 2%. Дослідження живуть за своїм лімітом.</summary>
    [Fact]
    public async Task HelpList_ShouldShowTheCapRaisedByTheClansTechnology()
    {
        var config = new GameConfigBuilder()
            .WithTechnologies(t => t.Clan.Nodes.Single(n => n.Key == "v_attack_all").Effects
                .Add(new TechnologyEffectConfig { Stat = TechnologyStat.HelpShareCap, PerLevel = 0.02 }))
            .Build();
        config.Clan = new ClanConfig { MaxHelpShare = 0.4, HelpSharePerPlayer = 0.02, MaxHelpShareWithTechnologies = 0.6 };
        var catalog = new GameCatalog(config);
        var clanId = Guid.NewGuid();

        var clanResearch = Substitute.For<IClanResearchRepository>();
        clanResearch.GetLevelsByClanAsync(clanId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyDictionary<string, int>?)new Dictionary<string, int> { ["1:v_attack_all"] = 2 });

        var request = new ClanHelpRequest(Guid.NewGuid(), 1, clanId, OwnerId, ClanHelpTarget.Construction, Guid.NewGuid(),
            TimeSpan.FromHours(1), Now.AddHours(1), Now);
        _clans.GetClanIdByMemberAsync(HelperId, Arg.Any<CancellationToken>()).Returns(clanId);
        _help.GetActiveByClanAsync(clanId, Now, Arg.Any<CancellationToken>()).Returns(new List<ClanHelpRequest> { request });

        var players = Substitute.For<IPlayerRepository>();
        players.GetNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [OwnerId] = "owner" });
        _research.GetActiveKeysAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());

        var handler = new GetClanHelpQueryHandler(_clans, _help, players, _research,
            TestTechnologies.ClanBonuses(catalog, clanResearch), catalog, new FakeTimeProvider(Now));

        var items = await handler.Handle(new GetClanHelpQuery(HelperId), CancellationToken.None);

        Assert.Equal(22, Assert.Single(items).MaxHelpers);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(0.04, 22)]
    [InlineData(0.20, 30)]
    [InlineData(5, 30)]
    public void MaxHelpers_ShouldGrowWithTheTechnology_UpToTheCeiling(double bonus, int helpers)
        => Assert.Equal(helpers, new ClanConfig { MaxHelpShare = 0.4, HelpSharePerPlayer = 0.02, MaxHelpShareWithTechnologies = 0.6 }.MaxHelpersWith(bonus));

    [Fact]
    public async Task HelpList_ShouldNameTheResearchedTechnology_AndLeaveOtherTargetsGeneric()
    {
        var config = new GameConfigBuilder().WithTechnologies().Build();
        var clanId = Guid.NewGuid();
        var players = Substitute.For<IPlayerRepository>();

        var research = new PlayerResearch(Guid.NewGuid(), OwnerId, 1, Now);
        research.Start(new TechnologyKey(2, "attack_spearman"), TimeSpan.FromHours(1), Now);

        var researchHelp = new ClanHelpRequest(Guid.NewGuid(), 1, clanId, OwnerId, ClanHelpTarget.Research, research.Id,
            research.PlannedDuration, Now.AddHours(1), Now);
        var constructionHelp = new ClanHelpRequest(Guid.NewGuid(), 1, clanId, OwnerId, ClanHelpTarget.Construction, Guid.NewGuid(),
            TimeSpan.FromHours(1), Now.AddHours(1), Now);

        _clans.GetClanIdByMemberAsync(HelperId, Arg.Any<CancellationToken>()).Returns(clanId);
        _help.GetActiveByClanAsync(clanId, Now, Arg.Any<CancellationToken>()).Returns([researchHelp, constructionHelp]);
        players.GetNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [OwnerId] = "owner" });
        _research.GetActiveKeysAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { research.Id })), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [research.Id] = research.ActiveKey! });

        var handler = new GetClanHelpQueryHandler(_clans, _help, players, _research,
            TestTechnologies.ClanBonuses(new GameCatalog(config)), new GameCatalog(config), new FakeTimeProvider(Now));

        var items = await handler.Handle(new GetClanHelpQuery(HelperId), CancellationToken.None);

        Assert.Equal("2:attack_spearman", items.Single(i => i.TargetType == ClanHelpTarget.Research).Subject);
        Assert.Null(items.Single(i => i.TargetType == ClanHelpTarget.Construction).Subject);
    }
}
