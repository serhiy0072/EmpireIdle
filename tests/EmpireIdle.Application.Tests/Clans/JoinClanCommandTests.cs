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

public class JoinClanCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid FounderId = Guid.NewGuid();
    private static readonly Guid ClanId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IClanRequestRepository _requests = Substitute.For<IClanRequestRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static GameConfig Config() => new()
    {
        // Каталог валідується на створенні й вимагає рівно одну головну
        // будівлю. Кланам вона не потрібна, але без неї GameCatalog не збереться
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Clan = new ClanConfig
        {
            Capacity = 200,
            RequestLifetimeHours = 72,
            RejectedCooldownHours = 12
        }
    };

    private JoinClanCommandHandler CreateHandler() => new(
        _clans, _requests, _players, _unitOfWork,
        new GameCatalog(Config()),
        new FakeTimeProvider(Now),
        NullLogger<JoinClanCommandHandler>.Instance);

    private static Player NewPlayer() => new(PlayerId, "tester", "tester@test.local", "user-1", Now);

    private static Clan NewClan(ClanJoinPolicy policy)
    {
        var clan = new Clan(ClanId, 1, "Alpha", "ALP", FounderId, Now);
        clan.UpdateSettings(FounderId, "opened for tests", policy, Now);

        return clan;
    }

    [Fact]
    public async Task Open_clan_adds_a_membership_row()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.Open);

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _requests.GetPendingForPlayerAsync(PlayerId, Now, Arg.Any<CancellationToken>()).Returns([]);

        var outcome = await CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        outcome.Should().Be(ClanJoinOutcome.Joined);

        // Регресія: раніше ставився лише Player.ClanId, а склад лишався порожнім
        clan.Members.Should().Contain(m => m.PlayerId == PlayerId);
        player.ClanId.Should().Be(ClanId);
    }

    [Fact]
    public async Task Approval_clan_creates_an_application_instead_of_joining()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.ByApproval);

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);

        var outcome = await CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        outcome.Should().Be(ClanJoinOutcome.ApplicationSubmitted);

        clan.Members.Should().NotContain(m => m.PlayerId == PlayerId);
        player.ClanId.Should().BeNull();

        await _requests.Received(1).AddAsync(
            Arg.Is<ClanRequest>(r => r.PlayerId == PlayerId
                                  && r.ClanId == ClanId
                                  && r.Kind == ClanRequestKind.Application
                                  && r.ExpiresAt == Now.AddHours(72)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invite_only_clan_rejects_a_direct_join()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.InviteOnly);

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);

        var act = () => CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Second_application_while_the_first_is_open_is_rejected()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.ByApproval);

        var pending = new ClanRequest(Guid.NewGuid(), 1, ClanId, PlayerId,
            ClanRequestKind.Application, Now.AddHours(70), Now.AddHours(-2));

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _requests.GetLatestAsync(ClanId, PlayerId, ClanRequestKind.Application, Arg.Any<CancellationToken>())
            .Returns(pending);

        var act = () => CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        await act.Should().ThrowAsync<AlreadyExistsException>();
    }

    [Fact]
    public async Task Reapplying_before_the_cooldown_expires_is_rejected()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.ByApproval);

        var declined = new ClanRequest(Guid.NewGuid(), 1, ClanId, PlayerId,
            ClanRequestKind.Application, Now.AddHours(60), Now.AddHours(-12));

        // Відмова годину тому, кулдаун 12 годин
        declined.Decline(FounderId, Now.AddHours(-1));

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _requests.GetLatestAsync(ClanId, PlayerId, ClanRequestKind.Application, Arg.Any<CancellationToken>())
            .Returns(declined);

        var act = () => CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        await act.Should().ThrowAsync<RequirementNotMetException>();

        await _requests.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Reapplying_after_the_cooldown_is_allowed()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.ByApproval);

        var declined = new ClanRequest(Guid.NewGuid(), 1, ClanId, PlayerId,
            ClanRequestKind.Application, Now.AddHours(-1), Now.AddHours(-40));

        declined.Decline(FounderId, Now.AddHours(-13));

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _requests.GetLatestAsync(ClanId, PlayerId, ClanRequestKind.Application, Arg.Any<CancellationToken>())
            .Returns(declined);

        var outcome = await CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        outcome.Should().Be(ClanJoinOutcome.ApplicationSubmitted);

        await _requests.ReceivedWithAnyArgs(1).AddAsync(default!, default);
    }

    [Fact]
    public async Task Joining_closes_other_pending_requests()
    {
        var player = NewPlayer();
        var clan = NewClan(ClanJoinPolicy.Open);

        var elsewhere = new ClanRequest(Guid.NewGuid(), 1, Guid.NewGuid(), PlayerId,
            ClanRequestKind.Application, Now.AddHours(50), Now.AddHours(-1));

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);
        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);
        _requests.GetPendingForPlayerAsync(PlayerId, Now, Arg.Any<CancellationToken>()).Returns([elsewhere]);

        await CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        // Інакше гравця можна прийняти вдруге, і Player.ClanId мовчки перезапишеться
        elsewhere.Status.Should().Be(ClanRequestStatus.Cancelled);
    }

    [Fact]
    public async Task Player_already_in_a_clan_cannot_join_another()
    {
        var player = NewPlayer();
        player.JoinClan(Guid.NewGuid());

        _players.GetByIdAsync(PlayerId, Arg.Any<CancellationToken>()).Returns(player);

        var act = () => CreateHandler().Handle(new JoinClanCommand(PlayerId, ClanId), default);

        await act.Should().ThrowAsync<InvalidStateException>();

        await _clans.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
