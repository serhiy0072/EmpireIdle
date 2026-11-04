using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

public class AllocateClanPointsCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid LeaderId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _research = Substitute.For<IClanResearchRepository>();
    private readonly IServerRepository _servers = Substitute.For<IServerRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly Clan _clan;
    private readonly ClanResearch _pool;
    private readonly GameCatalog _catalog;
    private readonly ClanTechnologies _technologies;

    public AllocateClanPointsCommandTests()
    {
        var config = new GameConfigBuilder().WithTechnologies().Build();
        _catalog = new GameCatalog(config);
        _technologies = new ClanTechnologies(config.Technologies.Clan);

        _clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", LeaderId, Now);
        _clan.Join(MemberId, capacity: 50, Now);
        _clans.GetByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_clan);

        _pool = new ClanResearch(Guid.NewGuid(), _clan.Id, 1, Now);
        _pool.AddPoints(100_000, Now);
        _research.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns(_pool);

        _serverContext.ServerId.Returns(1);
        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
    }

    private AllocateClanPointsCommandHandler Handler()
        => new(_clans, _research, _servers, _serverContext, _unitOfWork, _technologies,
            TestTechnologies.Settlement(_catalog, Substitute.For<IVillageRepository>()),
            new FakeTimeProvider(Now), NullLogger<AllocateClanPointsCommandHandler>.Instance);

    private Task<ClanAllocationResult> Allocate(Guid player, int tier, string node, int points)
        => Handler().Handle(new AllocateClanPointsCommand(player, tier, node, points), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldInvestPoints_AndReportTheClosedLevel()
    {
        var price = _technologies.Points(1, _technologies.Tree.Node("v_attack_all")!, 1);

        var result = await Allocate(LeaderId, 1, "v_attack_all", price);

        Assert.Equal("1:v_attack_all", Assert.Single(result.LevelsCompleted));
        Assert.Equal(100_000 - price, result.UnallocatedPoints);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheRoleCannotManageTechnologies()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Allocate(MemberId, 1, "v_attack_all", 10));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, error.Reason);
        Assert.Equal(100_000, _pool.UnallocatedPoints);
    }

    [Fact]
    public async Task Handle_ShouldNameThePrerequisites_WhenTheNodeIsNotOpenYet()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Allocate(LeaderId, 1, "c_coins", 10));

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, error.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuseTheTier_WhenTheWorldIsTooYoung()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Allocate(LeaderId, 2, "v_attack_all", 10));

        Assert.Equal(RefusalReasons.ClanTechTierLocked.Key, error.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheClanHasNeverOffered()
    {
        _research.GetByClanAsync(_clan.Id, Arg.Any<CancellationToken>()).Returns((ClanResearch?)null);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Allocate(LeaderId, 1, "v_attack_all", 10));

        Assert.Equal(RefusalReasons.ClanTechNotEnoughPoints.Key, error.Reason);
    }
}
