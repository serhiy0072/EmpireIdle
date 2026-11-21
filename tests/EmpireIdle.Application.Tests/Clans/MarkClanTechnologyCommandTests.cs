using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Clans.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Rewards;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Technologies;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>Мітку керівництва ставить лише роль із ManageTechnologies і лише на вузол, що приймає підношення.</summary>
public class MarkClanTechnologyCommandTests
{
    private static readonly DateTime Now = Entities.Now;
    private static readonly Guid LeaderId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly TechnologyKey Standard = new(1, "v_attack_all");
    private static readonly TechnologyKey Mint = new(1, "c_coins");

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanResearchRepository _research = Substitute.For<IClanResearchRepository>();
    private readonly IServerContext _serverContext = Substitute.For<IServerContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClanResearch _target;
    private readonly GameCatalog _catalog;
    private readonly ClanTechnologies _technologies;

    public MarkClanTechnologyCommandTests()
    {
        var config = new GameConfigBuilder().WithTechnologies().Build();
        _catalog = new GameCatalog(config);
        _technologies = new ClanTechnologies(config.Technologies.Clan);

        var clan = new Clan(Guid.NewGuid(), 1, "Alpha", "ALP", LeaderId, Now);
        clan.Join(MemberId, capacity: 50, Now);
        _clans.GetByMemberAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(clan);

        _target = new ClanResearch(Guid.NewGuid(), clan.Id, 1, Now);
        _research.GetByClanAsync(clan.Id, Arg.Any<CancellationToken>()).Returns(_target);

        _serverContext.ServerId.Returns(1);
    }

    private Task Mark(Guid playerId, TechnologyKey? key)
    {
        var offerings = new ClanOfferings(_clans, _research, Substitute.For<IClanOfferingRepository>(), _serverContext, _technologies,
            Substitute.For<IRandomSource>(), new RewardDispatcher([]),
            TestTechnologies.Settlement(_catalog, Substitute.For<IVillageRepository>()));

        return new MarkClanTechnologyCommandHandler(offerings, _unitOfWork, new FakeTimeProvider(Now),
                NullLogger<MarkClanTechnologyCommandHandler>.Instance)
            .Handle(new MarkClanTechnologyCommand(playerId, key?.Tier, key?.Node), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldMarkAnAvailableNode()
    {
        await Mark(LeaderId, Standard);

        Assert.True(_target.IsMarked(Standard));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldClearTheMark_WithoutANode()
    {
        _target.Mark(Standard, Now);

        await Mark(LeaderId, null);

        Assert.Null(_target.MarkedTechnology);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ARoleWithoutManageTechnologies()
    {
        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Mark(MemberId, Standard));

        Assert.Equal(RefusalReasons.ClanNoPermission.Key, error.Reason);
        Assert.Null(_target.MarkedTechnology);
    }

    /// <summary>Мітка на закритому вузлі обіцяла б надбавку за внесок, який неможливий.</summary>
    [Fact]
    public async Task Handle_ShouldRefuse_ANodeThatTakesNoOfferingsYet()
    {
        _target.Mark(Standard, Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Mark(LeaderId, Mint));

        Assert.Equal(RefusalReasons.ResearchPrerequisites.Key, error.Reason);
        Assert.True(_target.IsMarked(Standard));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AMaxedNode()
    {
        _target.Invest(Standard, 1_000_000, _technologies, Now);

        var error = await Assert.ThrowsAsync<RequirementNotMetException>(() => Mark(LeaderId, Standard));

        Assert.Equal(RefusalReasons.ResearchMaxed.Key, error.Reason);
    }
}
