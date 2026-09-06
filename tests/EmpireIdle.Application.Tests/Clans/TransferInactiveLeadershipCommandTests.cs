using AwesomeAssertions;
using EmpireIdle.Application.Clans.Commands;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Clans;

/// <summary>
/// Автопередача лідерства. Правило: зам, далі генерали, офіцери й нижче —
/// доки лідерство не перейде. Клан без лідера не має лишатись керованим
/// тим, хто не повернеться.
/// </summary>
public class TransferInactiveLeadershipCommandTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ClanId = Guid.NewGuid();
    private static readonly Guid LeaderId = Guid.NewGuid();

    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    /// <summary>Сім діб — саме стільки дає LeaderInactivityDays.</summary>
    private static readonly DateTime LongGone = Now.AddDays(-30);
    private static readonly DateTime Recently = Now.AddHours(-2);

    private static GameConfig Config() => new()
    {
        Buildings = [new BuildingConfig { Key = "townhall", IsMainBuilding = true, UpgradeCostGrowth = 1.45 }],
        Clan = new ClanConfig { Capacity = 200, LeaderInactivityDays = 7 }
    };

    private TransferInactiveLeadershipCommandHandler CreateHandler() => new(
        _clans, _players, _unitOfWork,
        new GameCatalog(Config()),
        new FakeTimeProvider(Now),
        NullLogger<TransferInactiveLeadershipCommandHandler>.Instance);

    /// <summary>Клан із лідером-засновником. Учасники додаються окремо.</summary>
    private Clan NewClan()
    {
        var clan = new Clan(ClanId, 1, "Alpha", "ALP", LeaderId, Now.AddMonths(-3));

        _clans.GetByIdAsync(ClanId, Arg.Any<CancellationToken>()).Returns(clan);

        return clan;
    }

    /// <summary>Додає учасника й одразу ставить йому роль за назвою.</summary>
    private static Guid AddMember(Clan clan, string roleName)
    {
        var playerId = Guid.NewGuid();

        clan.Join(playerId, 200, Now.AddMonths(-2));

        if (roleName != "Member")
        {
            var roleId = clan.Roles.Single(r => r.Name == roleName).Id;
            clan.AssignRole(LeaderId, playerId, roleId, Now.AddMonths(-2));
        }

        return playerId;
    }

    private void Presence(params (Guid PlayerId, DateTime LastSeen)[] entries)
        => _players.GetLastSeenAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(entries.ToDictionary(e => e.PlayerId, e => e.LastSeen));

    [Fact]
    public async Task Leadership_goes_to_the_deputy()
    {
        var clan = NewClan();
        var deputy = AddMember(clan, "Deputy");
        var officer = AddMember(clan, "Officer");

        Presence((LeaderId, LongGone), (deputy, Recently), (officer, Recently));

        var transferred = await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        transferred.Should().BeTrue();
        clan.LeaderId.Should().Be(deputy);

        // Колишній лідер не вилітає з клану, а стає другим за рангом
        clan.RoleOf(LeaderId)!.Name.Should().Be("Deputy");

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Without_a_deputy_leadership_goes_to_the_highest_officer()
    {
        var clan = NewClan();
        var general = AddMember(clan, "General");
        var officer = AddMember(clan, "Officer");
        var member = AddMember(clan, "Member");

        Presence((LeaderId, LongGone), (general, Recently), (officer, Recently), (member, Recently));

        await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        clan.LeaderId.Should().Be(general);
    }

    [Fact]
    public async Task Inactive_deputy_is_skipped_in_favour_of_an_active_officer()
    {
        var clan = NewClan();
        var deputy = AddMember(clan, "Deputy");
        var officer = AddMember(clan, "Officer");

        Presence((LeaderId, LongGone), (deputy, LongGone), (officer, Recently));

        await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        clan.LeaderId.Should().Be(officer);
    }

    [Fact]
    public async Task Descends_to_a_rank_and_file_member_when_nobody_above_is_active()
    {
        var clan = NewClan();
        var deputy = AddMember(clan, "Deputy");
        var officer = AddMember(clan, "Officer");
        var member = AddMember(clan, "Member");

        Presence((LeaderId, LongGone), (deputy, LongGone), (officer, LongGone), (member, Recently));

        await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        clan.LeaderId.Should().Be(member);
    }

    [Fact]
    public async Task Equal_ranks_are_broken_by_who_was_seen_last()
    {
        var clan = NewClan();
        var stale = AddMember(clan, "Officer");
        var fresh = AddMember(clan, "Officer");

        Presence((LeaderId, LongGone), (stale, Now.AddDays(-3)), (fresh, Recently));

        await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        clan.LeaderId.Should().Be(fresh);
    }

    [Fact]
    public async Task Leadership_still_moves_when_the_whole_clan_is_inactive()
    {
        var clan = NewClan();
        var deputy = AddMember(clan, "Deputy");
        var member = AddMember(clan, "Member");

        Presence((LeaderId, LongGone), (deputy, LongGone), (member, LongGone));

        var transferred = await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        // Правило доводиться до кінця: лідерство переходить за рангом
        transferred.Should().BeTrue();
        clan.LeaderId.Should().Be(deputy);
    }

    [Fact]
    public async Task Leader_who_came_back_keeps_the_clan()
    {
        var clan = NewClan();
        var deputy = AddMember(clan, "Deputy");

        // Джоб вибрав клан, але між вибіркою й командою лідер зайшов
        Presence((LeaderId, Recently), (deputy, Recently));

        var transferred = await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        transferred.Should().BeFalse();
        clan.LeaderId.Should().Be(LeaderId);

        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task Lone_leader_has_nobody_to_hand_over_to()
    {
        var clan = NewClan();

        Presence((LeaderId, LongGone));

        var transferred = await CreateHandler().Handle(new TransferInactiveLeadershipCommand(ClanId), default);

        transferred.Should().BeFalse();
        clan.LeaderId.Should().Be(LeaderId);
    }
}
