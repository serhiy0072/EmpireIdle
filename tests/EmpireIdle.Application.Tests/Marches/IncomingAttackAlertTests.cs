using EmpireIdle.Application.Common.Events;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Application.Marches.EventHandlers;
using EmpireIdle.Application.Marches.Queries;
using EmpireIdle.Application.Marches.ReadModels;
using EmpireIdle.Application.Marches.Services;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Marches;

/// <summary>
/// Тривога про напад: кому летить подія, коли її знімають і що бачить захисник після
/// перезавантаження. Захисник у клані — тривожимо весь клан; поза кланом — лише його самого.
/// </summary>
public class IncomingAttackAlertTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMarchRepository _marches = Substitute.For<IMarchRepository>();
    private readonly IVillageRepository _villages = Substitute.For<IVillageRepository>();
    private readonly IClanStructureRepository _structures = Substitute.For<IClanStructureRepository>();
    private readonly IPlayerRepository _players = Substitute.For<IPlayerRepository>();
    private readonly IClanRepository _clans = Substitute.For<IClanRepository>();
    private readonly IGameNotifier _notifier = Substitute.For<IGameNotifier>();

    private DefenderAudience Audience() => new(_villages, _structures, _clans, _players);

    private HostileMarchLaunchedHandler LaunchedHandler()
        => new(_marches, Audience(), _notifier, NullLogger<HostileMarchLaunchedHandler>.Instance);

    private static IncomingAttack Attack(Guid targetId, MarchTargetType targetType = MarchTargetType.Village)
        => new(Guid.NewGuid(), MarchIntent.Attack, targetType, targetId, "Ціль", null, null, 10, 10, 0, 0,
            Guid.NewGuid(), "Нападник", "WAR", Now, Now.AddMinutes(15));

    private static DomainEventNotification<HostileMarchLaunched> Launched(IncomingAttack attack)
        => new(new HostileMarchLaunched(attack.MarchId, attack.TargetType, attack.TargetId, attack.ArrivesAt, Now));

    private Village GivenVillage(Guid ownerId, Guid? clanId)
    {
        var village = new Village(Guid.NewGuid(), ownerId, "Ціль", [], 10, 10);

        _villages.GetByIdAsync(village.Id, Arg.Any<CancellationToken>()).Returns(village);
        _clans.GetClanIdByMemberAsync(ownerId, Arg.Any<CancellationToken>()).Returns(clanId);

        return village;
    }

    [Fact]
    public async Task Alert_ShouldGoToTheWholeClan_WhenTheDefenderIsInOne()
    {
        var owner = Guid.NewGuid();
        var clanId = Guid.NewGuid();
        var village = GivenVillage(owner, clanId);
        var attack = Attack(village.Id);
        IReadOnlyList<Guid> members = [owner, Guid.NewGuid(), Guid.NewGuid()];

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);
        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);

        await LaunchedHandler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)), attack, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Alert_ShouldReachTheOwner_WhenTheDefenderHasNoClan()
    {
        var owner = Guid.NewGuid();
        var village = GivenVillage(owner, clanId: null);
        var attack = Attack(village.Id);

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);

        await LaunchedHandler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { owner })), attack, Arg.Any<CancellationToken>());
        await _players.DidNotReceiveWithAnyArgs().GetIdsByClanAsync(default, default);
    }

    [Fact]
    public async Task Alert_ShouldGoToTheOwningClan_ForAStructure()
    {
        var clanId = Guid.NewGuid();
        var structure = new ClanStructure(Guid.NewGuid(), 1, clanId, 5, 5, Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero, Now);
        var attack = Attack(structure.Id, MarchTargetType.ClanStructure);
        IReadOnlyList<Guid> members = [Guid.NewGuid(), Guid.NewGuid()];

        _structures.GetByIdAsync(structure.Id, Arg.Any<CancellationToken>()).Returns(structure);
        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns(attack);
        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);

        await LaunchedHandler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.Received(1).NotifyAttackIncomingAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)), attack, Arg.Any<CancellationToken>());
    }

    /// <summary>Поки outbox дійшов, марш уже прибув — тривога запізніла, мовчимо.</summary>
    [Fact]
    public async Task Alert_ShouldStaySilent_WhenTheMarchIsNoLongerOnItsWay()
    {
        var attack = Attack(GivenVillage(Guid.NewGuid(), null).Id);

        _marches.GetIncomingAttackAsync(attack.MarchId, Arg.Any<CancellationToken>()).Returns((IncomingAttack?)null);

        await LaunchedHandler().Handle(Launched(attack), CancellationToken.None);

        await _notifier.DidNotReceiveWithAnyArgs().NotifyAttackIncomingAsync(default!, default!, default);
    }

    /// <summary>Марш розвернувся в дорозі — ті самі захисники знімають тривогу.</summary>
    [Fact]
    public async Task CalledOff_ShouldReachTheSameDefenders()
    {
        var owner = Guid.NewGuid();
        var clanId = Guid.NewGuid();
        var village = GivenVillage(owner, clanId);
        IReadOnlyList<Guid> members = [owner, Guid.NewGuid()];
        var marchId = Guid.NewGuid();

        _players.GetIdsByClanAsync(clanId, Arg.Any<CancellationToken>()).Returns(members);

        await new HostileMarchCalledOffHandler(Audience(), _notifier).Handle(
            new DomainEventNotification<HostileMarchCalledOff>(
                new HostileMarchCalledOff(marchId, MarchTargetType.Village, village.Id, Now)),
            CancellationToken.None);

        await _notifier.Received(1).NotifyAttackCalledOffAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(members)), marchId, Arg.Any<CancellationToken>());
    }

    /// <summary>Ціль зникла — тривожити нікого.</summary>
    [Fact]
    public async Task Audience_ShouldBeEmpty_WhenTheTargetIsGone()
    {
        var recipients = await Audience().ResolveAsync(MarchTargetType.ClanStructure, Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(recipients);
    }

    [Fact]
    public async Task Query_ShouldCoverTheWholeClan_AndItsStructures()
    {
        var player = Guid.NewGuid();
        var clanId = Guid.NewGuid();
        IReadOnlyList<Guid> members = [player, Guid.NewGuid()];
        List<IncomingAttack> attacks = [Attack(Guid.NewGuid())];

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
