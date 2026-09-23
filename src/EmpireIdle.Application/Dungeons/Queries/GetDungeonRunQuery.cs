using EmpireIdle.Application.Common.Security;
using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Application.Dungeons.Services;
using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Dungeons.Queries
{
    /// <summary>
    /// Поточний забіг гравця з розгорнутим станом бою. Потрібен після
    /// перезавантаження сторінки: клієнт відновлює бій із нуля, а не тримає
    /// його в пам'яті вкладки.
    /// </summary>
    public record GetDungeonRunQuery(Guid PlayerId) : IRequest<DungeonRunView?>, IPlayerScopedRequest;

    internal sealed class GetDungeonRunQueryHandler : IRequestHandler<GetDungeonRunQuery, DungeonRunView?>
    {
        private readonly IDungeonRepository _dungeons;
        private readonly BattleBuilder _builder;
        private readonly GameCatalog _catalog;

        public GetDungeonRunQueryHandler(IDungeonRepository dungeons, BattleBuilder builder, GameCatalog catalog)
        {
            _dungeons = dungeons;
            _builder = builder;
            _catalog = catalog;
        }

        public async Task<DungeonRunView?> Handle(GetDungeonRunQuery request, CancellationToken cancellationToken)
        {
            var run = await _dungeons.GetActiveRunAsync(request.PlayerId, cancellationToken);

            if (run is null)
                return null;

            var state = BattleSerializer.Read(run.Battle);

            return Project(run.Id, run.DungeonKey, run.Level, run.State, state,
                _builder.WaveCount(run.Level), turns: [], reward: null, _catalog);
        }

        /// <summary>
        /// Стан бою у вигляді для клієнта: назви, вміння й готовність кожного
        /// з них. Готовність рахує сервер, бо саме він відмовить у ході.
        /// </summary>
        public static DungeonRunView Project(Guid runId, string dungeonKey, int level, DungeonRunState state,
            BattleState battle, int waveCount, IReadOnlyList<TurnLog> turns, DungeonRewardView? reward, GameCatalog catalog)
        {
            var dungeon = catalog.Dungeons.GetValueOrDefault(dungeonKey);

            var combatants = battle.Combatants
                .Select(c =>
                {
                    var hero = c.HeroId is null ? null : catalog.FindHero(c.Key);

                    var abilities = (hero?.Abilities ?? [])
                        .Select(a => new AbilityView(a.Key, a.DisplayName, a.Description, a.EnergyCost,
                            a.Target.ToString(), c.Energy >= a.EnergyCost, a.IgnoresLine))
                        .ToList();

                    var enemyName = dungeon?.Waves.Concat(dungeon.Boss).FirstOrDefault(e => e.Key == c.Key)?.DisplayName;

                    return new CombatantView(
                        c.Index,
                        c.Side.ToString(),
                        c.Line.ToString(),
                        c.Key,
                        hero?.DisplayName ?? enemyName ?? c.Key,
                        c.HeroId,
                        c.Attack,
                        c.Defense,
                        c.Health,
                        c.MaxHealth,
                        c.ShieldPoints,
                        c.Speed,
                        c.Energy,
                        c.Statuses.Select(s => new StatusView(s.Kind.ToString(), s.Magnitude, s.TurnsLeft)).ToList(),
                        abilities);
                })
                .ToList();

            return new DungeonRunView(
                runId,
                dungeonKey,
                level,
                state.ToString(),
                battle.Wave,
                waveCount,
                state == DungeonRunState.InProgress ? BattleEngine.CurrentActor(battle) : null,
                combatants,
                turns,
                reward);
        }
    }
}
