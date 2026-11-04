using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Application.Research.Services;
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
/// Технологія виробітку клану не діє заднім числом: ні на вступі, ні на закритті рівня накопичене не
/// перераховується за новою ставкою. Ферма — 10 їжі за хвилину; v_attack_all тут дає ще +50% їжі за рівень.
/// </summary>
public class ClanProductionSettlementTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid LeaderId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");

    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _clanResearch = Substitute.For<IClanResearchRepository>();
    private readonly IClanOfferingRepository _offerings = Substitute.For<IClanOfferingRepository>();
    private readonly IClanRequestRepository _requests = Substitute.For<IClanRequestRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IRandomSource _random = Substitute.For<IRandomSource>();
    private readonly GameCatalog _catalog;
    private readonly ClanTechnologies _technologies;

    public ClanProductionSettlementTests()
    {
        var config = new GameConfigBuilder()
            .WithMap(m => m.Geometry = new MapGeometryConfig
            {
                RingBoundaries = [0.5], RingMultipliers = [1.0, 1.0], RingsAtFirstLevel = 1.0, FogMinShare = 1.0, FogMaxShare = 1.0
            })
            .WithBuildings(TestKeys.Farm, TestKeys.Warehouse)
            .WithTechnologies(t => t.Clan.Nodes.Single(n => n.Key == "v_attack_all").Effects
                .Add(new TechnologyEffectConfig { Stat = TechnologyStat.Production, Target = TestKeys.Food, PerLevel = 0.5 }))
            .Build();

        var farm = config.Buildings.Single(b => b.Key == TestKeys.Farm);
        farm.ProducesResource = TestKeys.Food;
        farm.BaseProductionPerMinute = 10;
        farm.BaseStorage = 1_000_000;

        _catalog = new GameCatalog(config);
        _technologies = new ClanTechnologies(config.Technologies.Clan);

        _servers.GetLevelAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(1);
        _serverContext.ServerId.Returns(1);
        _random.NextDouble().Returns(0.5);
        _requests.GetPendingForPlayerAsync(Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ClanRequest>());
    }

    private ResearchSettlement Settlement()
    {
        var research = Substitute.For<IPlayerResearchRepository>();

        return new ResearchSettlement(_villages, _servers,
            TestEffects.Resolver(Substitute.For<IActiveEffectRepository>(), catalog: _catalog, research: research, clanResearch: _clanResearch),
            new WorldGeometry(_catalog.Config.Map), _catalog, new PersonalTechnologies(_catalog.Config.Technologies.Personal));
    }

    /// <summary>Село гравця з фермою, що виробляє годину.</summary>
    private Building GivenFarm(Guid playerId)
    {
        var village = new Village(Guid.NewGuid(), playerId, "Test", [TestKeys.Food, TestKeys.Gold], 0, 0);
        village.AddBuilding(TestKeys.Townhall, _catalog.Buildings, Now.AddHours(-1));
        village.AddBuilding(TestKeys.Farm, _catalog.Buildings, Now.AddHours(-1));
        village.GrantResources([new ResourceCost { Resource = TestKeys.Gold, Amount = 100_000 }], Now.AddHours(-1));
        _villages.GetByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(village);

        return village.Buildings.Single(b => b.Type == TestKeys.Farm);
    }

    private void GivenClanLevels(Guid playerId, int level)
        => _clanResearch.GetLevelsByMemberAsync(playerId, Arg.Any<CancellationToken>())
            .Returns(level == 0
                ? null
                : (IReadOnlyDictionary<string, int>?)new Dictionary<string, int> { [Standard.ToString()] = level });

    [Fact]
    public async Task Join_ShouldBankTheHourWithoutTheClansBonus()
    {
        var farm = GivenFarm(PlayerId);
        GivenClanLevels(PlayerId, 0);

        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", LeaderId, Now);
        clan.UpdateSettings(LeaderId, "", ClanJoinPolicy.Open, Now);
        _clans.GetByIdAsync(clan.Id, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>())
            .Returns(new Player(PlayerId, "player", "player@test.local", "u-player", Now));

        await new JoinClanCommandHandler(_clans, _requests, _players, _unitOfWork, _catalog, new FakeTimeProvider(Now),
                Settlement(), NullLogger<JoinClanCommandHandler>.Instance)
            .Handle(new JoinClanCommand(PlayerId, clan.Id), CancellationToken.None);

        // 60 хв × 10/хв: +50% клану діють лише від вступу
        Assert.Equal(600, farm.AccruedAmount);
        Assert.Equal(Now, farm.LastAccruedAt);
        Assert.Contains(clan.Members, m => m.PlayerId == PlayerId);
    }

    /// <summary>Вихід банкує годину ще з бонусом: зберігається те, що клан уже дав.</summary>
    [Fact]
    public async Task Settlement_ShouldBankTheHourWithTheClansBonus_WhileStillAMember()
    {
        var farm = GivenFarm(PlayerId);
        GivenClanLevels(PlayerId, 1);

        await Settlement().SettleVillageAsync(PlayerId, Now, CancellationToken.None);

        Assert.Equal(900, farm.AccruedAmount);
    }

    [Fact]
    public async Task OfferingThatClosesAProductionLevel_ShouldBankEveryMembersVillage_AtTheOldRate()
    {
        var offererFarm = GivenFarm(LeaderId);
        var memberFarm = GivenFarm(PlayerId);
        GivenClanLevels(LeaderId, 0);
        GivenClanLevels(PlayerId, 0);

        var (clan, research) = GivenClanOneOfferingShortOfALevel();

        var result = await Offer(clan, research);

        Assert.Single(result.LevelsCompleted);
        Assert.Equal(600, offererFarm.AccruedAmount);
        Assert.Equal(600, memberFarm.AccruedAmount);
        Assert.Equal(Now, memberFarm.LastAccruedAt);
    }

    /// <summary>Звичайне підношення чужих сіл не чіпає.</summary>
    [Fact]
    public async Task OfferingThatClosesNothing_ShouldLeaveOtherVillagesAlone()
    {
        GivenFarm(LeaderId);
        var memberFarm = GivenFarm(PlayerId);
        GivenClanLevels(LeaderId, 0);
        GivenClanLevels(PlayerId, 0);

        var (clan, research) = GivenClanOneOfferingShortOfALevel();
        research.SelectTarget(new TechnologyKey(1, "c_coins"), Now);

        await Offer(clan, research);

        Assert.Equal(0, memberFarm.AccruedAmount);
    }

    private (Clan Clan, ClanResearch Research) GivenClanOneOfferingShortOfALevel()
    {
        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", LeaderId, Now);
        clan.Join(PlayerId, capacity: 50, Now);
        _clans.GetByMemberAsync(LeaderId, Arg.Any<CancellationToken>()).Returns(clan);

        var research = new ClanResearch(Guid.NewGuid(), clan.Id, 1, Now);
        research.SelectTarget(Standard, Now);
        research.AddPoints(_technologies.Points(1, _technologies.Tree.Node(Standard.Node)!, 1) - 10, _technologies, Now);
        _clanResearch.GetByClanAsync(clan.Id, Arg.Any<CancellationToken>()).Returns(research);

        return (clan, research);
    }

    private Task<ClanOfferingResult> Offer(Clan clan, ClanResearch research)
    {
        var offerings = new ClanOfferings(_clans, _clanResearch, _offerings, _serverContext, _technologies, _random,
            new RewardDispatcher([]), Settlement());

        return new OfferResourcesCommandHandler(offerings, _villages, _unitOfWork, _technologies, new VillageStatus(_catalog),
                new FakeTimeProvider(Now), NullLogger<OfferResourcesCommandHandler>.Instance)
            .Handle(new OfferResourcesCommand(LeaderId, TestKeys.Gold), CancellationToken.None);
    }
}
