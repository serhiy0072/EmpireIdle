using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Queries;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EmpireIdle.Application.Dungeons.Commands
{
    /// <summary>
    /// Один хід забігу.
    ///
    /// Один запит — один хід: саме тому перемкнутися між автобоєм і ручним
    /// керуванням можна будь-якої миті, і сервер не має гадати, чи гравець
    /// іще дивиться. Ходи ворогів та героїв в автобою рахуються тут само,
    /// поки черга не дійде до героя під ручним керуванням.
    /// </summary>
    /// <param name="Auto">
    /// true — сервер грає бій сам до кінця. false — прокручує лише ходи ворогів
    /// і зупиняється перед ходом героя; якщо передано дію, виконує саме її.
    /// </param>
    public record TakeDungeonTurnCommand(Guid PlayerId, Guid RunId, bool Auto, string? AbilityKey, int? TargetIndex)
        : IRequest<DungeonRunView>, IPlayerScopedRequest;

    /// <param name="Artifacts">Ключі виданих артефактів — назви додає проєкція з каталогу.</param>
    public record DungeonReward(IReadOnlyList<ResourceCost> Resources, IReadOnlyList<string> Artifacts);

    internal sealed class TakeDungeonTurnCommandHandler : IRequestHandler<TakeDungeonTurnCommand, DungeonRunView>
    {
        /// <summary>Стеля ходів на запит: захист від нескінченного циклу, а не ігрове правило.</summary>
        private const int MaxTurnsPerRequest = 200;

        private readonly IDungeonRepository _dungeons;
        private readonly BattleEngine _engine;
        private readonly BattleBuilder _builder;
        private readonly DungeonRewarder _rewarder;
        private readonly GameCatalog _catalog;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<TakeDungeonTurnCommandHandler> _logger;

        public TakeDungeonTurnCommandHandler(
            IDungeonRepository dungeons,
            BattleEngine engine,
            BattleBuilder builder,
            DungeonRewarder rewarder,
            GameCatalog catalog,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider,
            ILogger<TakeDungeonTurnCommandHandler> logger)
        {
            _dungeons = dungeons;
            _engine = engine;
            _builder = builder;
            _rewarder = rewarder;
            _catalog = catalog;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<DungeonRunView> Handle(TakeDungeonTurnCommand request, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var run = await _dungeons.GetRunByIdAsync(request.RunId, cancellationToken)
                ?? throw new EntityNotFoundException("Dungeon run", request.RunId.ToString());

            if (run.PlayerId != request.PlayerId)
                throw new EntityNotFoundException("Dungeon run", request.RunId.ToString());

            if (run.State != DungeonRunState.InProgress)
                throw new InvalidStateException($"Dungeon run {run.Id} is already {run.State}.");

            var dungeon = _catalog.Dungeons.GetValueOrDefault(run.DungeonKey)
                ?? throw new EntityNotFoundException("Dungeon", run.DungeonKey);

            var state = BattleSerializer.Read(run.Battle);
            var turns = new List<TurnLog>();

            // Дія гравця задана, якщо він назвав ціль або вміння; інакше запит
            // у ручному режимі означає «прокрути ходи ворогів і зупинись»
            var hasPlayerAction = !request.Auto && (request.TargetIndex is not null || request.AbilityKey is not null);

            for (var guard = 0; guard < MaxTurnsPerRequest; guard++)
            {
                state = EnsureRound(state);

                if (BattleEngine.CurrentActor(state) is not { } actorIndex)
                    break;

                var actor = state.Combatants[actorIndex];
                var heroTurn = actor.Side == BattleSide.Heroes;

                // Хід героя в ручному режимі належить гравцю: або виконуємо його
                // дію, або зупиняємось і повертаємо керування
                if (heroTurn && !request.Auto && !hasPlayerAction)
                    break;

                var abilities = heroTurn ? AbilitiesOf(actor.Key) : [];
                var manual = heroTurn && !request.Auto;

                var action = manual
                    ? new BattleAction(request.AbilityKey, request.TargetIndex)
                    : _engine.ChooseAuto(state, actorIndex, abilities);

                var ability = Resolve(abilities, action.AbilityKey, actor, manual);

                if (manual)
                {
                    ValidateManual(state, actor, ability, action);
                }

                var result = _engine.Execute(state, actorIndex, action, ability);

                state = result.State;
                turns.Add(result.Log);

                if (!state.EnemiesAlive || !state.HeroesAlive)
                    break;

                // Ручний режим після ходу героя віддає керування назад гравцю
                if (manual)
                    break;
            }

            // Хвиля скінчилась — або наступна, або кінець забігу
            if (!state.EnemiesAlive && state.HeroesAlive)
            {
                if (state.Wave < _builder.WaveCount(run.Level))
                    state = _builder.NextWave(state, dungeon, run.Level);
                else
                    return await FinishAsync(run, state, turns, won: true, now, cancellationToken);
            }

            if (!state.HeroesAlive)
                return await FinishAsync(run, state, turns, won: false, now, cancellationToken);

            state = EnsureRound(state);
            run.Advance(BattleSerializer.Write(state), now);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Project(run, state, turns, reward: null);
        }

        private async Task<DungeonRunView> FinishAsync(DungeonRun run, BattleState state,
            List<TurnLog> turns, bool won, DateTime now, CancellationToken cancellationToken)
        {
            var battle = BattleSerializer.Write(state);
            DungeonReward? reward = null;

            if (won)
            {
                run.Win(battle, now);
                reward = await _rewarder.GrantAsync(run, now, cancellationToken);
            }
            else
            {
                run.Lose(battle, now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Dungeon run {RunId} finished as {State} on wave {Wave}",
                run.Id, run.State, state.Wave);

            return Project(run, state, turns, reward);
        }

        /// <summary>
        /// Одна форма відповіді на всі виклики данжів: і перегляд, і хід віддають
        /// той самий DungeonRunView. Дві різні форми змушували б клієнт зшивати
        /// сирий стан бою з назвами — саме там і народжуються розбіжності.
        /// </summary>
        private DungeonRunView Project(DungeonRun run, BattleState state, IReadOnlyList<TurnLog> turns, DungeonReward? reward)
        {
            var view = reward is null
                ? null
                : new DungeonRewardView(
                    reward.Resources.Select(r => new RewardLine(r.Resource, r.Amount)).ToList(),
                    reward.Artifacts
                        .Select(key => _catalog.FindItem(key))
                        .Where(item => item is not null)
                        .Select(item => new ArtifactDropView(
                            item!.Key, item.DisplayName, item.Rarity.ToString().ToLowerInvariant(), item.SetKey ?? string.Empty))
                        .ToList());

            return GetDungeonRunQueryHandler.Project(run.Id, run.DungeonKey, run.Level, run.State, state,
                _builder.WaveCount(run.Level), turns, view, _catalog);
        }

        /// <summary>Порожня черга означає кінець раунду — шикуємо новий.</summary>
        private static BattleState EnsureRound(BattleState state)
            => state.Queue.Any(i => state.Combatants[i].IsAlive) ? state : BattleEngine.NextRound(state);

        private IReadOnlyList<Domain.Services.Config.HeroAbilityConfig> AbilitiesOf(string heroKey)
            => _catalog.FindHero(heroKey)?.Abilities ?? [];

        private static Domain.Services.Config.HeroAbilityConfig? Resolve(
            IReadOnlyList<Domain.Services.Config.HeroAbilityConfig> abilities, string? key, Combatant actor, bool manual)
        {
            if (key is null)
                return null;

            var ability = abilities.FirstOrDefault(a => a.Key == key);

            if (ability is null && manual)
                throw new EntityNotFoundException("Hero ability", key);

            return ability;
        }

        /// <summary>
        /// Ручний хід перевіряється тут: енергія, жива ціль і правило ліній.
        /// Клієнт малює те саме, але вирішує сервер.
        /// </summary>
        private static void ValidateManual(BattleState state, Combatant actor,
            Domain.Services.Config.HeroAbilityConfig? ability, BattleAction action)
        {
            if (ability is not null && actor.Energy < ability.EnergyCost)
                throw new RequirementNotMetException($"Ability '{ability.Key}' needs {ability.EnergyCost} energy.");

            var needsTarget = ability is null
                || ability.Target is AbilityTarget.SingleEnemy or AbilityTarget.SingleAlly;

            if (!needsTarget)
                return;

            if (action.TargetIndex is not { } target)
                throw new RequirementNotMetException("This action needs a target.");

            if (!BattleEngine.CanTarget(state, actor, ability, target))
                throw new RequirementNotMetException("That target cannot be reached right now.");
        }
    }
}
