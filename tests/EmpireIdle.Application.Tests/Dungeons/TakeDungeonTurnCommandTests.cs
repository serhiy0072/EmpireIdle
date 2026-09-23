using EmpireIdle.Application.Common.Exceptions;
using EmpireIdle.Application.Common.Services;
using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
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
/// Причина існування: щоб бій можна було дивитися й перемикати режим
/// посеред нього, один запит має грати рівно один хід — і в автобою теж.
/// Інакше перший же автоматичний запит дограє забіг до кінця.
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

    /// <param name="finish">Довести забіг до кінця ще до ходу — для повторів по завершеному забігу.</param>
    private TakeDungeonTurnCommandHandler Handler(BattleState state, Action<DungeonRun, string>? finish = null)
    {
        var config = new GameConfigBuilder().WithDungeons().Build();
        var catalog = new GameCatalog(config);

        var battle = BattleSerializer.Write(state);
        var run = new DungeonRun(RunId, PlayerId, 1, TestKeys.Dungeon, 1, battle, Now);
        finish?.Invoke(run, battle);

        _dungeons.GetRunByIdAsync(RunId, Arg.Any<CancellationToken>()).Returns(run);

        var granter = new ItemGranter(
            Substitute.For<IInventoryRepository>(),
            Substitute.For<IServerContext>(),
            Substitute.For<IRandomSource>(),
            new ArtifactRoller(config.Equipment));

        var rewarder = new DungeonRewarder(_dungeons, Substitute.For<IVillageRepository>(), granter,
            new BattleBuilder(config.Dungeons), catalog, Substitute.For<IRandomSource>());

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
    public async Task Handle_ManualWithoutAction_ShouldPlayTheEnemyAndStopBeforeHero()
    {
        var handler = Handler(Battle());

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: false, AbilityKey: null, TargetIndex: null),
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
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: false, AbilityKey: null, TargetIndex: 1),
            CancellationToken.None);

        Assert.Single(result.Turns);
        Assert.Equal(0, result.Turns[0].ActorIndex);
        Assert.True(result.Combatants[1].Health < 100_000);
    }

    [Fact]
    public async Task Handle_Auto_ShouldPlayExactlyOneTurn()
    {
        var handler = Handler(Battle());

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        // Хід зіграв лише ворог — герой лишився на черзі й дочекається
        // наступного запиту, хай навіть уже в ручному режимі
        Assert.Single(result.Turns);
        Assert.Equal(1, result.Turns[0].ActorIndex);
        Assert.Equal(0, result.ActorIndex);
    }

    [Fact]
    public async Task Handle_ManualWithoutAction_OnAHeroTurn_ShouldChangeNothing()
    {
        var handler = Handler(Battle() with { Queue = [0] });

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: false, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Empty(result.Turns);
        Assert.Equal(0, result.ActorIndex);
    }

    /// <summary>
    /// Ціль за передньою лінією — відмова з причиною: клієнт дзеркалить правило,
    /// але якщо розійдеться, гравець побачить пояснення, а не англійський Detail.
    /// </summary>
    [Fact]
    public async Task Handle_ManualAtABackLineTarget_ShouldRefuseWithTheReason()
    {
        var state = Battle();
        var backLine = state.Combatants[1] with { Index = 2, Line = BattleLine.Back };
        var handler = Handler(state with { Combatants = [.. state.Combatants, backLine], Queue = [0] });

        var refusal = await Assert.ThrowsAsync<RequirementNotMetException>(() => handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: false, AbilityKey: null, TargetIndex: 2),
            CancellationToken.None));

        Assert.Equal(RefusalReasons.DungeonTargetUnreachable.Key, refusal.Reason);
    }

    // ---------- Захист номером ходу ----------

    /// <summary>
    /// Клієнт діє зі стану, якого вже немає (втрачена відповідь, друга вкладка):
    /// хід не грається, стан не пишеться — інакше дія влучила б не туди.
    /// </summary>
    [Fact]
    public async Task Handle_WithAStaleExpectedTurn_ShouldRejectWithoutPlaying()
    {
        var handler = Handler(Battle() with { TurnNumber = 7 });

        await Assert.ThrowsAsync<StaleTurnException>(() => handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 6, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None));

        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnTheNextTurnNumber_ForTheClientToEchoBack()
    {
        var handler = Handler(Battle() with { TurnNumber = 7 });

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 7, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(8, result.TurnNumber);
    }

    /// <summary>Повтор ходу, що вже завершив забіг, віддає підсумок, а не помилку — і нічого не пише.</summary>
    [Fact]
    public async Task Handle_OnAFinishedRun_ShouldReturnTheOutcomeWithoutPlaying()
    {
        var handler = Handler(Battle(), (run, battle) => run.Win(battle, Now));

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(nameof(DungeonRunState.Won), result.State);
        Assert.Empty(result.Turns);
        Assert.Null(result.Reward);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---------- Стеля раундів (MaxRoundsPerWave = 30 у фікстурі) ----------

    /// <summary>Останній хід 30-го раунду нікого не вбив — забіг закривається нічиєю, а не висить.</summary>
    [Fact]
    public async Task Handle_WhenTheLastAllowedRoundEnds_ShouldTimeOutWithoutReward()
    {
        var handler = Handler(Battle() with { Round = 30, Queue = [0] });

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(nameof(DungeonRunState.TimedOut), result.State);
        Assert.Single(result.Turns);
        Assert.Null(result.Reward);
        Assert.Null(result.ActorIndex);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Зачищена хвиля важить більше за стелю: нова хвиля рахує раунди з нуля.</summary>
    [Fact]
    public async Task Handle_WhenTheWaveIsClearedOnTheLastRound_ShouldMoveOnInsteadOfTimingOut()
    {
        var state = Battle();
        state = state with
        {
            Round = 30,
            Queue = [0],
            Combatants = [state.Combatants[0], state.Combatants[1] with { Health = 1 }],
        };

        var result = await Handler(state).Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(nameof(DungeonRunState.InProgress), result.State);
        Assert.Equal(2, result.Wave);
    }

    /// <summary>Загибель команди на останньому ході — поразка, а не нічия.</summary>
    [Fact]
    public async Task Handle_WhenTheTeamFallsOnTheLastRound_ShouldLoseRatherThanTimeOut()
    {
        var state = Battle();
        state = state with
        {
            Round = 30,
            Queue = [1],
            Combatants = [state.Combatants[0] with { Health = 1 }, state.Combatants[1]],
        };

        var result = await Handler(state).Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(nameof(DungeonRunState.Lost), result.State);
    }

    /// <summary>Забіг, що застряг до появи стелі, закривається першим же запитом — без зайвого ходу.</summary>
    [Fact]
    public async Task Handle_OnARunAlreadyPastTheCeiling_ShouldTimeOutWithoutPlaying()
    {
        var handler = Handler(Battle() with { Round = 57, Queue = [0] });

        var result = await handler.Handle(
            new TakeDungeonTurnCommand(PlayerId, RunId, ExpectedTurn: 0, Auto: true, AbilityKey: null, TargetIndex: null),
            CancellationToken.None);

        Assert.Equal(nameof(DungeonRunState.TimedOut), result.State);
        Assert.Empty(result.Turns);
    }
}
