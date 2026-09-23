using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using EmpireIdle.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EmpireIdle.Application.Tests.Dungeons;

/// <summary>
/// Межа між автобоєм і ручним керуванням.
///
/// Причина існування: запит із auto=true прокручує бій до кінця, тож у
/// ручному режимі ходи ворогів довелося просити окремо — і зробити це
/// так, щоб сервер зупинився перед ходом героя, а не догравав за гравця.
/// </summary>
public class TakeDungeonTurnCommandTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();

    private readonly IDungeonRepository _dungeons = Substitute.For<IDungeonRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    /// <summary>Герой повільніший за ворога — тож черга завжди починається з ворога.</summary>
    private static BattleState Battle() => BattleEngine.NextRound(new BattleState
    {
        Combatants =
        [
            new Combatant
            {
                Index = 0,
                Side = BattleSide.Heroes,
                Line = BattleLine.Front,
                Key = TestKeys.CommonHero,
                HeroId = Guid.NewGuid(),
                Attack = 60,
                Defense = 30,
                MaxHealth = 100_000,
                Health = 100_000,
                Speed = 5,
                Energy = 0,
                Statuses = [],
                UniqueStats = [],
            },
            new Combatant
            {
                Index = 1,
                Side = BattleSide.Enemies,
                Line = BattleLine.Front,
                Key = TestKeys.DungeonEnemy,
                Attack = 40,
                Defense = 20,
                MaxHealth = 100_000,
                Health = 100_000,
                Speed = 20,
                Energy = 0,
                Statuses = [],
                UniqueStats = [],
            },
        ],
        Wave = 1,
        Round = 0,
        Queue = [],
        Seed = 42,
        TurnNumber = 0,
    });

    private TakeDungeonTurnCommandHandler Handler(BattleState state)
    {
        var config = new GameConfigBuilder().WithDungeons().Build();
        var catalog = new GameCatalog(config);

        _dungeons.GetRunByIdAsync(RunId, Arg.Any<CancellationToken>())
            .Returns(new DungeonRun(RunId, PlayerId, 1, TestKeys.Dungeon, 1, BattleSerializer.Write(state), Now));

        var granter = new ItemGranter(
            Substitute.For<IInventoryRepository>(),
            Substitute.For<IServerContext>(),
            Substitute.For<IRandomSource>(),
            new ArtifactRoller(config.Equipment));

        var rewarder = new DungeonRewarder(_dungeons, Substitute.For<IVillageRepository>(), granter, catalog,
            Substitute.For<IRandomSource>());

        return new TakeDungeonTurnCommandHandler(
            _dungeons,
            new BattleEngine(config.Dungeons),
            new BattleBuilder(config.Dungeons),
            rewarder,
            catalog,
            _unitOfWork,
            new FakeTimeProvider(Now),
            NullLogger<TakeDungeonTurnCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ManualWithoutAction_ShouldPlayEnemiesAndStopBeforeHero()
    {
        var handler = Handler(Battle());

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, Auto: false, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Single(result.Turns);
        Assert.Equal(1, result.Turns[0].ActorIndex);
        Assert.Equal(0, result.ActorIndex);
    }

    [Fact]
    public async Task Handle_ManualWithTarget_ShouldPlayOnlyTheHeroTurn()
    {
        // Ворог уже сходив: черга стоїть на героєві
        var state = Battle() with { Queue = [0] };
        var handler = Handler(state);

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, Auto: false, AbilityKey: null, TargetIndex: 1),
            CancellationToken.None);

        Assert.Single(result.Turns);
        Assert.Equal(0, result.Turns[0].ActorIndex);
        Assert.True(result.Combatants[1].Health < 100_000);
    }

    [Fact]
    public async Task Handle_Auto_ShouldPlayBothSides()
    {
        var handler = Handler(Battle());

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Contains(result.Turns, t => t.ActorIndex == 0);
        Assert.Contains(result.Turns, t => t.ActorIndex == 1);
    }
}
