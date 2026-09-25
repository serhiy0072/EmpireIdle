using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.EventHandlers;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Application.Marches.ReadModels;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Тривога про напад: кому летить подія і що бачить захисник після перезавантаження.
/// Захисник у клані — тривожимо весь клан; поза кланом — лише його самого.
/// </summary>
public class IncomingAttackAlertTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();

    private static IncomingAttack Attack(Guid? ownerId, Guid? clanId, MarchTargetType targetType = MarchTargetType.Village)
        => new(Guid.NewGuid(), targetType, Guid.NewGuid(), "Ціль", ownerId, clanId, 10, 10, 0, 0,
            Guid.NewGuid(), "Нападник", "WAR", Now, Now.AddMinutes(15));

    private HostileMarchLaunchedHandler Handler()
        => new(_marches, _players, _notifier, NullLogger<HostileMarchLaunchedHandler>.Instance);

    private static DomainEventNotification<HostileMarchLaunched> Launched(IncomingAttack attack)
        => new(new HostileMarchLaunched(attack.MarchId, attack.TargetType, attack.TargetId, attack.ArrivesAt, Now));

    [Fact]
    public async Task Alert_ShouldGoToTheWholeClan_WhenTheDefenderIsInOne()
    {
        var owner = Guid.NewGuid();
        var clanId = Guid.NewGuid();
        var attack = Attack(owner, clanId);
        IReadOnlyList<Guid> members = [owner, Guid.NewGuid(), Guid.NewGuid()];

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);
        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);

        await Handler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)), attack, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Alert_ShouldReachTheOwner_WhenTheDefenderHasNoClan()
    {
        var owner = Guid.NewGuid();
        var attack = Attack(owner, clanId: null);

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);

        await Handler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { owner })), attack, Arg.Any<CancellationToken>());
        await _players.DidNotReceiveWithAnyArgs().GetIdsByClanAsync(default, default);
    }

    [Fact]
    public async Task Alert_ShouldGoToTheOwningClan_ForAStructure()
    {
        var clanId = Guid.NewGuid();
        var attack = Attack(ownerId: null, clanId, MarchTargetType.ClanStructure);
        IReadOnlyList<Guid> members = [Guid.NewGuid(), Guid.NewGuid()];

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);
        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);

        await Handler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)), attack, Arg.Any<CancellationToken>());
    }

    /// <summary>Поки outbox дійшов, марш уже прибув — тривога запізніла, мовчимо.</summary>
    [Fact]
    public async Task Alert_ShouldStaySilent_WhenTheMarchIsNoLongerOnItsWay()
    {
        var attack = Attack(Guid.NewGuid(), Guid.NewGuid());

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns((IncomingAttack?)null);

        await Handler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.DidNotReceiveWithAnyArgs().NotifyAttackIncomingAsync(default!, default!, default);
    }

    [Fact]
    public async Task Query_ShouldCoverTheWholeClan_AndItsStructures()
    {
        var player = Guid.NewGuid();
        var clanId = Guid.NewGuid();
        IReadOnlyList<Guid> members = [player, Guid.NewGuid()];
        List<IncomingAttack> attacks = [Attack(player, clanId)];

        _clans.GetClanIdByMemberAsync(player, Arg.Any<CancellationToken>()).Returns(clanId);
        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);
        _marches.GetIncomingAttacksAsync(members, clanId, Arg.Any<CancellationToken>()).Returns(attacks);

        var result = await new GetIncomingAttacksQueryHandler(_clans, _players, _marches)
            .Handle(new GetIncomingAttacksQuery(player), CancellationToken.None);

        Assert.Same(attacks, result);
    }

    [Fact]
    public async Task Query_ShouldCoverOnlyThePlayer_OutsideAClan()
    {
        var player = Guid.NewGuid();

        _clans.GetClanIdByMemberAsync(player, Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _marches.GetIncomingAttacksAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await new GetIncomingAttacksQueryHandler(_clans, _players, _marches)
            .Handle(new GetIncomingAttacksQuery(player), CancellationToken.None);

        await _marches.Received(1).GetIncomingAttacksAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { player })), null, Arg.Any<CancellationToken>());
    }
}
