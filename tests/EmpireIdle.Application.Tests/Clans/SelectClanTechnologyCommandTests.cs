using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

public class SelectClanTechnologyCommandTests
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

    public SelectClanTechnologyCommandTests()
    {
        _clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", LeaderId, Now);
        _clan.Join(MemberId, capacity: 50, Now);
        _clans.GetByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_clan);
        _serverContext.ServerId.Returns(1);
        _servers.GetLevelAsync(1, Arg.Any<CancellationToken>()).Returns(1);
    }

    private SelectClanTechnologyCommandHandler Handler()
        => new(_clans, _research, _servers, _serverContext, _unitOfWork,
            new ClanTechnologies(new GameConfigBuilder().WithTechnologies().Build().Technologies.Clan),
            new FakeTimeProvider(Now), NullLogger<SelectClanTechnologyCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ShouldCreateTheResearchAndSetTheTarget_WhenTheClanHadNone()
    {
        await Handler().Handle(new SelectClanTechnologyCommand(LeaderId, 1, "v_attack_all"), CancellationToken.None);

        await _research.Received(1).AddAsync(
            Arg.Is<ClanResearch>(r => r.ClanId == _clan.Id && r.TargetKey == "1:v_attack_all"), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheRoleCannotManageTechnologies()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => Handler().Handle(new SelectClanTechnologyCommand(MemberId, 1, "v_attack_all"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, error.Reason);
    }

    [Fact]
    public async Task Handle_ShouldNameThePrerequisites_WhenTheNodeIsNotOpenYet()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => Handler().Handle(new SelectClanTechnologyCommand(LeaderId, 1, "c_coins"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, error.Reason);
    }

    [Fact]
    public async Task Handle_ShouldRefuseTheTier_WhenTheWorldIsTooYoung()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(
            () => Handler().Handle(new SelectClanTechnologyCommand(LeaderId, 2, "v_attack_all"), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanTechTierLocked.Key, error.Reason);
    }
}
