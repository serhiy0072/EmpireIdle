using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>
    /// Герой, уже порахований застосунком: стати з рівня, тіру й спорядження,
    /// вміння — під їхній рівень (лише відкриті й ті, що діють у данжі).
    /// </summary>
    /// <param name="BattleStats">Данжові властивості з наборів і заточки: частками, перезарядка — цілими ходами.</param>
    public record DungeonHero(
        Guid HeroId,
        string HeroKey,
        string HeroClass,
        double Attack,
        double Defense,
        double Health,
        double Speed,
        IReadOnlyDictionary<DungeonStat, double> BattleStats,
        IReadOnlyList<CombatSkill> Skills);

    /// <summary>
    /// Складає бій: ставить героїв у лінії за класом і вирощує ворогів хвилі
    /// під рівень забігу. Окремо від рушія, бо рушій має лишатися чистим
    /// рахунком ходу й не знати ні про конфіг данжів, ні про склад команди.
    /// </summary>
    public class BattleBuilder
    {
        private readonly DungeonsConfig _config;

        public BattleBuilder(DungeonsConfig config)
        {
            _config = config;
        }

        /// <summary>Скільки хвиль у забігу цього рівня; остання — бос.</summary>
        public int WaveCount(int level) => _config.BaseWaves + level;

        /// <summary>Множник стат ворогів за рівень забігу.</summary>
        public double PowerMultiplier(int level)
            => _config.LevelPowerMultipliers.ElementAtOrDefault(level - 1) is var m && m > 0
                ? m
                : _config.LevelPowerMultipliers.LastOrDefault(1.0);

        /// <summary>Множник ресурсної нагороди за рівень забігу.</summary>
        public double RewardMultiplier(int level)
            => _config.LevelRewardMultipliers.ElementAtOrDefault(level - 1) is var m && m > 0
                ? m
                : _config.LevelRewardMultipliers.LastOrDefault(1.0);

        /// <summary>Досвід героїв за успішний забіг цього рівня — той самий множник, що й у ресурсів.</summary>
        public long HeroExperience(int level)
            => (long)Math.Round(_config.HeroExperiencePerRun * RewardMultiplier(level));

        public BattleLine LineFor(string heroClass)
            => _config.FrontLineClasses.Contains(heroClass) ? BattleLine.Front : BattleLine.Back;

        /// <summary>
        /// Перша хвиля забігу: герої на повному здоров'ї, вміння на повній перезарядці.
        /// </summary>
        public BattleState Start(IReadOnlyList<DungeonHero> team, DungeonConfig dungeon, int level, int seed)
            => BuildWave(team.Select(ToCombatant).ToList(), dungeon, level, wave: 1, seed, turnNumber: 0);

        /// <summary>
        /// Наступна хвиля: здоров'я, перезарядки й стани команди переносяться як є —
        /// саме тому тримати готове вміння до боса є сенсом.
        /// </summary>
        public BattleState NextWave(BattleState state, DungeonConfig dungeon, int level)
        {
            var heroes = state.Combatants
                .Where(c => c.Side == BattleSide.Heroes)
                .ToList();

            return BuildWave(heroes, dungeon, level, state.Wave + 1, state.Seed, state.TurnNumber);
        }

        private BattleState BuildWave(List<Combatant> heroes, DungeonConfig dungeon, int level, int wave, int seed, int turnNumber)
        {
            var isBoss = wave == WaveCount(level);
            var multiplier = PowerMultiplier(level);

            var roster = isBoss
                ? dungeon.Boss
                : dungeon.Waves.Where(e => e.Wave == wave).ToList();

            // Данж може мати менше описаних хвиль, ніж дає рівень: беремо останню наявну
            if (roster.Count == 0)
                roster = dungeon.Waves.Where(e => e.Wave == dungeon.Waves.Max(x => x.Wave)).ToList();

            var enemies = roster
                .Select((enemy, position) => new Combatant
                {
                    Index = heroes.Count + position,
                    Side = BattleSide.Enemies,
                    Line = enemy.Line,
                    Key = enemy.Key,
                    Attack = Math.Round(enemy.Attack * multiplier, 1),
                    Defense = Math.Round(enemy.Defense * multiplier, 1),
                    MaxHealth = Math.Round(enemy.Health * multiplier, 1),
                    Health = Math.Round(enemy.Health * multiplier, 1),
                    Speed = enemy.Speed,
                    Statuses = [],
                    UniqueStats = [],
                })
                .ToList();

            var state = new BattleState
            {
                Combatants = [.. heroes, .. enemies],
                Wave = wave,
                Round = 0,
                Queue = [],
                Seed = seed,
                TurnNumber = turnNumber,
            };

            return BattleEngine.NextRound(state);
        }

        /// <summary>
        /// Герой на старті забігу. Оберіг ставить щит одразу — на WardTurns власних ходів, як щит уміння:
        /// перший удар хвилі приймає він, а не здоров'я.
        /// </summary>
        private Combatant ToCombatant(DungeonHero hero, int index)
        {
            var health = Math.Round(hero.Health, 1);
            var ward = Math.Round(health * Math.Min(_config.MaxWard, hero.BattleStats.GetValueOrDefault(DungeonStat.Ward)), 1);

            return new Combatant
            {
                Index = index,
                Side = BattleSide.Heroes,
                Line = LineFor(hero.HeroClass),
                Key = hero.HeroKey,
                HeroId = hero.HeroId,
                Attack = Math.Round(hero.Attack, 1),
                Defense = Math.Round(hero.Defense, 1),
                MaxHealth = health,
                Health = health,
                Speed = hero.Speed,
                ShieldPoints = ward,
                Statuses = ward > 0 ? [new BattleStatus { Kind = BattleStatusKind.Shield, Magnitude = 0, TurnsLeft = Math.Max(1, _config.WardTurns) }] : [],
                UniqueStats = hero.BattleStats.ToDictionary(p => p.Key, p => p.Value),
                Skills = hero.Skills.ToList(),
            };
        }
    }
}
