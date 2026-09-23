using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>Герой, уже порахований застосунком: стати з рівня, тіру й спорядження.</summary>
    public record DungeonHero(
        Guid HeroId,
        string HeroKey,
        string HeroClass,
        double Attack,
        double Defense,
        double Health,
        double Speed,
        IReadOnlyDictionary<DungeonStat, double> UniqueStats);

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

        public BattleLine LineFor(string heroClass)
            => _config.FrontLineClasses.Contains(heroClass) ? BattleLine.Front : BattleLine.Back;

        /// <summary>
        /// Перша хвиля забігу: герої на повному здоров'ї з порожньою шкалою.
        /// </summary>
        public BattleState Start(IReadOnlyList<DungeonHero> team, DungeonConfig dungeon, int level, int seed)
            => BuildWave(team.Select(ToCombatant).ToList(), dungeon, level, wave: 1, seed, turnNumber: 0);

        /// <summary>
        /// Наступна хвиля: здоров'я, енергія й стани команди переносяться як є —
        /// саме тому тримати сильне вміння до боса є сенсом.
        /// </summary>
        public BattleState NextWave(BattleState state, DungeonConfig dungeon, int level)
        {
            var heroes = state.Combatants
                .Where(c => c.Side == BattleSide.Heroes)
                .Select(c => c with { Index = c.Index })
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
                    Energy = 0,
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

        private Combatant ToCombatant(DungeonHero hero, int index) => new()
        {
            Index = index,
            Side = BattleSide.Heroes,
            Line = LineFor(hero.HeroClass),
            Key = hero.HeroKey,
            HeroId = hero.HeroId,
            Attack = Math.Round(hero.Attack, 1),
            Defense = Math.Round(hero.Defense, 1),
            MaxHealth = Math.Round(hero.Health, 1),
            Health = Math.Round(hero.Health, 1),
            Speed = hero.Speed,
            Energy = 0,
            Statuses = [],
            UniqueStats = hero.UniqueStats.ToDictionary(p => p.Key, p => p.Value),
        };
    }
}
