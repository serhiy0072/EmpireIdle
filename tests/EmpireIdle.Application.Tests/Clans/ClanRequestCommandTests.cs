using AwesomeAssertions;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

public class ClanRequestCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid LeaderId = Guid.NewGuid();
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid OutsiderId = Guid.NewGuid();
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanRequestRepository _requests = Substitute.For<IClanRequestRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Clan = new ClanConfig { Capacity = 200, RequestLifetimeHours = 72, RejectedCooldownHours = 12 }
    };

    private ResolveClanRequestCommandHandler Resolver() => new(
        _clans, _requests, _players, _unitOfWork,
        new GameCatalog(Config()),
        new FakeTimeProvider(Now),
        NullLogger<ResolveClanRequestCommandHandler>.Instance);

    private CancelClanRequestCommandHandler Canceller() => new(
        _clans, _requests, _unitOfWork,
        new FakeTimeProvider(Now),
        NullLogger<CancelClanRequestCommandHandler>.Instance);

    private static Player NewPlayer(Guid id) => new(id, "tester", $"{id:N}@test.local", $"user-{id:N}", Now);

    /// <summary>Клан із засновником-лідером і рядовим учасником без права Recruit.</summary>
    private static Clan NewClan()
    {
        var clan = new Clan(ClanId, 1, "Alpha", "ALP", LeaderId, Now);
        clan.Join(MemberId, 200, Now);

        return clan;
    }

    private ClanRequest Application(Guid playerId, DateTime? expiresAt = null)
    {
        var request = new ClanRequest(Guid.NewGuid(), 1, ClanId, playerId,
            ClanRequestKind.Application, expiresAt ?? Now.AddHours(70), Now.AddHours(-2));

        _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(NewClan());

        return request;
    }

    private ClanRequest Invite(Guid playerId)
    {
        var request = new ClanRequest(Guid.NewGuid(), 1, ClanId, playerId,
            ClanRequestKind.Invite, Now.AddHours(70), Now.AddHours(-2));

        _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(NewClan());

        return request;
    }

    [Fact]
    public async Task Approved_application_adds_the_applicant_to_the_clan()
    {
        var applicant = NewPlayer(OutsiderId);
        var application = Application(OutsiderId);

        var clan = NewClan();
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(OutsiderId, Arg.Any<CancellationToken>()).Returns(applicant);
        _requests.GetPendingForPlayerAsync(OutsiderId, Now, Arg.Any<CancellationToken>()).Returns([application]);

        await Resolver().Handle(new ResolveClanRequestCommand(LeaderId, application.Id, true), default);

        application.Status.Should().Be(ClanRequestStatus.Accepted);
        clan.Members.Should().Contain(m => m.PlayerId == OutsiderId);
        applicant.ClanId.Should().Be(ClanId);
    }

    [Fact]
    public async Task Declined_application_leaves_the_clan_untouched()
    {
        var application = Application(OutsiderId);

        var clan = NewClan();
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);

        await Resolver().Handle(new ResolveClanRequestCommand(LeaderId, application.Id, false), default);

        application.Status.Should().Be(ClanRequestStatus.Declined);
        application.ResolvedBy.Should().Be(LeaderId);
        clan.Members.Should().NotContain(m => m.PlayerId == OutsiderId);

        await _players.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }

    [Fact]
    public async Task Member_without_recruit_permission_cannot_resolve_an_application()
    {
        var application = Application(OutsiderId);

        var act = () => Resolver().Handle(new ResolveClanRequestCommand(MemberId, application.Id, true), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        application.Status.Should().Be(ClanRequestStatus.Pending);
    }

    [Fact]
    public async Task Someone_elses_invite_looks_like_it_does_not_exist()
    {
        var invite = Invite(OutsiderId);

        // 404, не 403: інакше перебором id видно, кого куди кличуть
        var act = () => Resolver().Handle(new ResolveClanRequestCommand(MemberId, invite.Id, true), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();

        invite.Status.Should().Be(ClanRequestStatus.Pending);
    }

    [Fact]
    public async Task Accepted_invite_puts_the_invited_player_into_the_clan()
    {
        var invited = NewPlayer(OutsiderId);
        var invite = Invite(OutsiderId);

        var clan = NewClan();
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(OutsiderId, Arg.Any<CancellationToken>()).Returns(invited);
        _requests.GetPendingForPlayerAsync(OutsiderId, Now, Arg.Any<CancellationToken>()).Returns([invite]);

        await Resolver().Handle(new ResolveClanRequestCommand(OutsiderId, invite.Id, true), default);

        invite.Status.Should().Be(ClanRequestStatus.Accepted);
        clan.Members.Should().Contain(m => m.PlayerId == OutsiderId);
    }

    [Fact]
    public async Task Applicant_who_joined_elsewhere_is_not_admitted()
    {
        var applicant = NewPlayer(OutsiderId);
        applicant.JoinClan(Guid.NewGuid());

        var application = Application(OutsiderId);

        var clan = NewClan();
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(OutsiderId, Arg.Any<CancellationToken>()).Returns(applicant);

        var act = () => Resolver().Handle(new ResolveClanRequestCommand(LeaderId, application.Id, true), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        application.Status.Should().Be(ClanRequestStatus.Pending);
        clan.Members.Should().NotContain(m => m.PlayerId == OutsiderId);
    }

    [Fact]
    public async Task Expired_application_cannot_be_approved()
    {
        var applicant = NewPlayer(OutsiderId);
        var application = Application(OutsiderId, Now.AddHours(-1));

        var clan = NewClan();
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _players.GetByIdAsync(OutsiderId, Arg.Any<CancellationToken>()).Returns(applicant);

        var act = () => Resolver().Handle(new ResolveClanRequestCommand(LeaderId, application.Id, true), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        clan.Members.Should().NotContain(m => m.PlayerId == OutsiderId);
    }

    [Fact]
    public async Task Accepting_one_request_cancels_the_rest()
    {
        var applicant = NewPlayer(OutsiderId);
        var application = Application(OutsiderId);

        var elsewhere = new ClanRequest(Guid.NewGuid(), 1, Guid.NewGuid(), OutsiderId,
            ClanRequestKind.Application, Now.AddHours(50), Now.AddHours(-1));

        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(NewClan());
        _players.GetByIdAsync(OutsiderId, Arg.Any<CancellationToken>()).Returns(applicant);
        _requests.GetPendingForPlayerAsync(OutsiderId, Now, Arg.Any<CancellationToken>())
            .Returns([application, elsewhere]);

        await Resolver().Handle(new ResolveClanRequestCommand(LeaderId, application.Id, true), default);

        elsewhere.Status.Should().Be(ClanRequestStatus.Cancelled);
        application.Status.Should().Be(ClanRequestStatus.Accepted);
    }

    [Fact]
    public async Task Applicant_withdraws_their_own_application()
    {
        var application = Application(OutsiderId);

        await Canceller().Handle(new CancelClanRequestCommand(OutsiderId, application.Id), default);

        application.Status.Should().Be(ClanRequestStatus.Cancelled);
    }

    [Fact]
    public async Task Only_the_applicant_can_withdraw_their_application()
    {
        var application = Application(OutsiderId);

        var act = () => Canceller().Handle(new CancelClanRequestCommand(MemberId, application.Id), default);

        await act.Should().ThrowAsync<EntityNotFoundException>();

        application.Status.Should().Be(ClanRequestStatus.Pending);
    }

    [Fact]
    public async Task Officer_revokes_an_invite_the_clan_sent()
    {
        var invite = Invite(OutsiderId);

        await Canceller().Handle(new CancelClanRequestCommand(LeaderId, invite.Id), default);

        invite.Status.Should().Be(ClanRequestStatus.Cancelled);
    }

    [Fact]
    public async Task Member_without_recruit_permission_cannot_revoke_an_invite()
    {
        var invite = Invite(OutsiderId);

        var act = () => Canceller().Handle(new CancelClanRequestCommand(MemberId, invite.Id), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        invite.Status.Should().Be(ClanRequestStatus.Pending);
    }
}

