using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Territory.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Лідер виходить із клану: є ще хтось — лідерство переходить найвищому за рангом
/// (серед рівних — найсвіжішому в грі); лишився сам — клан розпускається.
/// </summary>
public class LeaveClanCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaderId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IGarrisonRepository _garrisons = Substitute.For<IGarrisonRepository>();
    private readonly IHeroRepository _heroes = Substitute.For<IHeroRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IClanRequestRepository _requests = Substitute.For<IClanRequestRepository>();
    private readonly IClanHelpRepository _helps = Substitute.For<IClanHelpRepository>();
    private readonly IClanQuestRepository _quests = Substitute.For<IClanQuestRepository>();
    private readonly IMapRepository _map = Substitute.For<IMapRepository>();

    // Окремий стаб у тесті має перекривати цей — тому він у конструкторі, а не в Handler()
    public LeaveClanCommandTests() =>
        _structures.GetByClanAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<ClanStructure>());

    private LeaveClanCommandHandler Handler()
    {
        var config = new GameConfig
        {
            Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true }],
            Map = new MapConfig
            {
                Width = 100, Height = 100, TerrainSeed = 1,
                Terrains = [new TerrainConfig { Type = "plain", Weight = 1, Passable = true, MoveCost = 1.0, Habitable = true }]
            }
        };
        var catalog = new GameCatalog(config);

        // Повернення військ тут не перевіряється — їх у сцені немає
        _garrisons.GetHoldingReinforcementsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Garrison>());
        _heroes.GetForeignGarrisonIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var returner = new ReinforcementReturner(_garrisons, _villages, _structures,
            Substitute.For<IMarchRepository>(), _heroes, new MarchCalculator(new TerrainGenerator(config.Map), catalog),
            catalog, new HeroProgression(config.HeroSettings), TestEffects.Resolver(Substitute.For<IActiveEffectRepository>()), NullLogger<ReinforcementReturner>.Instance);

        var structureRemover = new ClanStructureRemover(_structures, _garrisons, _map, returner,
            NullLogger<ClanStructureRemover>.Instance);
        var disbander = new ClanDisbander(_clans, _structures, _requests, _helps, _quests, structureRemover,
            NullLogger<ClanDisbander>.Instance);

        return new LeaveClanCommandHandler(_clans, _players, _villages, _unitOfWork, new FakeTimeProvider(Now),
            returner, new ClanSuccession(_players), disbander, NullLogger<LeaveClanCommandHandler>.Instance);
    }

    private Clan GivenClan(params (Guid Id, string Role, DateTime LastSeen)[] members)
    {
        var clan = new Clan(Guid.NewGuid(), 1, "Вовки", "WLF", LeaderId, Now.AddDays(-30));
        var leader = new Player(LeaderId, "leader", "leader@test.local", "u-leader", Now.AddDays(-30));
        leader.JoinClan(clan.Id);

        foreach (var (id, role, _) in members)
        {
            clan.Join(id, capacity: 50, Now.AddDays(-10));

            if (role != "default")
                clan.AssignRole(LeaderId, id, clan.Roles.Single(r => r.Name == role).Id, Now.AddDays(-5));
        }

        _clans.GetByMemberAsync(LeaderId, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(LeaderId, Arg.Any<CancellationToken>()).Returns(leader);
        _players.GetLastSeenAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(members.ToDictionary(m => m.Id, m => m.LastSeen));

        return clan;
    }

    [Fact]
    public async Task Handle_ShouldPassLeadershipToTheHighestRank_WhenTheLeaderLeaves()
    {
        var rookie = Guid.NewGuid();
        var deputy = Guid.NewGuid();
        var deputyRole = new Clan(Guid.NewGuid(), 1, "x", "X", Guid.NewGuid(), Now).Roles
            .Where(r => !r.IsLeaderRole).OrderByDescending(r => r.Rank).First().Name;
        var clan = GivenClan((rookie, "default", Now), (deputy, deputyRole, Now.AddDays(-3)));

        await Handler().Handle(new LeaveClanCommand(LeaderId), CancellationToken.None);

        Assert.True(clan.IsLeader(deputy));
        Assert.DoesNotContain(clan.Members, m => m.PlayerId == LeaderId);
        _clans.DidNotReceive().Remove(clan);
    }

    /// <summary>Серед рівних за рангом — той, хто був у грі найпізніше.</summary>
    [Fact]
    public async Task Handle_ShouldPreferTheFreshestAmongEqualRanks()
    {
        var stale = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        var clan = GivenClan((stale, "default", Now.AddDays(-9)), (fresh, "default", Now.AddHours(-1)));

        await Handler().Handle(new LeaveClanCommand(LeaderId), CancellationToken.None);

        Assert.True(clan.IsLeader(fresh));
    }

    /// <summary>Засновник-одинак виходить — клан розпускається, назва й тег звільняються.</summary>
    [Fact]
    public async Task Handle_ShouldDisbandTheClan_WhenTheLeaderWasAlone()
    {
        var clan = GivenClan();

        await Handler().Handle(new LeaveClanCommand(LeaderId), CancellationToken.None);

        Assert.Empty(clan.Members);
        _clans.Received(1).Remove(clan);
    }

    /// <summary>
    /// Споруди, заявки, запити допомоги й прогрес квестів тримаються за клан лише значенням
    /// ClanId — каскад БД їх не бачить, тож розпуск прибирає їх сам і в одній транзакції.
    /// </summary>
    [Fact]
    public async Task Handle_ShouldRemoveEverythingTiedToTheClan_WhenItIsDisbanded()
    {
        var clan = GivenClan();
        var structure = new ClanStructure(Guid.NewGuid(), 1, clan.Id, 10, 10, Guid.NewGuid(), LeaderId, TimeSpan.Zero, Now);
        _structures.GetByClanAsync(clan.Id, Arg.Any<CancellationToken>()).Returns(new List<ClanStructure> { structure });

        await Handler().Handle(new LeaveClanCommand(LeaderId), CancellationToken.None);

        _structures.Received(1).Remove(structure);
        await _requests.Received(1).RemoveByClanAsync(clan.Id, Arg.Any<CancellationToken>());
        await _helps.Received(1).RemoveByClanAsync(clan.Id, Arg.Any<CancellationToken>());
        await _quests.Received(1).RemoveByClanAsync(clan.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldKeepClanData_WhenTheClanSurvives()
    {
        var clan = GivenClan((Guid.NewGuid(), "default", Now));

        await Handler().Handle(new LeaveClanCommand(LeaderId), CancellationToken.None);

        await _requests.DidNotReceive().RemoveByClanAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }
}
