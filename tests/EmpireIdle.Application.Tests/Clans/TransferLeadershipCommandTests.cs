using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Добровільна передача лідерства: лише лідер і лише учаснику клану. Колишній
/// лідер лишається в клані на другій за рангом ролі.
/// </summary>
public class TransferLeadershipCommandTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaderId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private TransferLeadershipCommandHandler Handler() => new(_clans, _unitOfWork, new FakeTimeProvider(Now),
        NullLogger<TransferLeadershipCommandHandler>.Instance);

    private Clan GivenClan(Guid member)
    {
        var clan = new Clan(Guid.NewGuid(), 1, "Вовки", "WLF", LeaderId, Now.AddDays(-30));
        clan.Join(member, capacity: 50, Now.AddDays(-10));

        _clans.GetByMemberAsync(LeaderId, Arg.Any<CancellationToken>()).Returns(clan);
        _clans.GetByMemberAsync(member, Arg.Any<CancellationToken>()).Returns(clan);

        return clan;
    }

    [Fact]
    public async Task Handle_ShouldMakeTheTargetLeader_AndKeepTheOldLeaderInTheClan()
    {
        var member = Guid.NewGuid();
        var clan = GivenClan(member);

        await Handler().Handle(new TransferLeadershipCommand(LeaderId, member), CancellationToken.None);

        Assert.True(clan.IsLeader(member));
        Assert.False(clan.IsLeader(LeaderId));
        Assert.Contains(clan.Members, m => m.PlayerId == LeaderId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheActorIsNotTheLeader()
    {
        var member = Guid.NewGuid();
        GivenClan(member);

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() =>
            Handler().Handle(new TransferLeadershipCommand(member, LeaderId), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanLeaderOnly.Key, refusal.Reason);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRefuse_ATargetOutsideTheClan()
    {
        GivenClan(Guid.NewGuid());

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            Handler().Handle(new TransferLeadershipCommand(LeaderId, Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ShouldRefuse_AnActorWithoutAClan()
    {
        var refusal = await Assert.ThrowsAsync<InvalidStateException>(() =>
            Handler().Handle(new TransferLeadershipCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(RefusalReasons.ClanNotMember.Key, refusal.Reason);
    }
}
