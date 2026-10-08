using EmpireIdle.Application.Dungeons.Contracts;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;

namespace EmpireIdle.Application.Dungeons.Services
{
    /// <summary>
    /// Стан бою у вигляді для клієнта: назви, вміння й готовність кожного
    /// з них. Готовність рахує сервер, бо саме він відмовить у ході.
    ///
    /// Окремо від обробників: і перегляд, і хід віддають той самий
    /// DungeonRunView, а команда не має залежати від обробника запиту.
    /// </summary>
    public static class DungeonRunProjection
    {
        public static DungeonRunView Project(Guid runId, string dungeonKey, int level, DungeonRunState state,
            BattleState battle, int waveCount, IReadOnlyList<TurnLog> turns, DungeonRewardView? reward, GameCatalog catalog)
        {
            var dungeon = catalog.Dungeons.GetValueOrDefault(dungeonKey);

            var combatants = battle.Combatants
                .Select(c =>
                {
                    var hero = c.HeroId is null ? null : catalog.FindHero(c.Key);

                    // Назви — з конфіга героя, а стан перезарядки — з бою: вміння зняте на старті забігу
                    var skills = c.Skills
                        .Select(s =>
                        {
                            var config = hero?.Skills.FirstOrDefault(x => x.Key == s.Key);

                            return new CombatSkillView(s.Key, config?.DisplayName ?? s.Key, config?.Description ?? string.Empty,
                                s.Kind.ToString(), s.Target.ToString(), s.Cooldown, s.CooldownLeft, s.Ready, s.IgnoresLine);
                        })
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
                        c.Statuses.Select(s => new StatusView(s.Kind.ToString(), s.Magnitude, s.TurnsLeft)).ToList(),
                        skills);
                })
                .ToList();

            return new DungeonRunView(
                runId,
                dungeonKey,
                level,
                state.ToString(),
                battle.Wave,
                waveCount,
                battle.TurnNumber,
                state == DungeonRunState.InProgress ? BattleEngine.CurrentActor(battle) : null,
                combatants,
                turns,
                reward);
        }
    }
}
