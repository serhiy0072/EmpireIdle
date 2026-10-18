using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Одномоментний бій арени й вежі (рішення 16–18.10.2026): усі б'ють усіх за таймером атаки, поки
    /// одна сторона не ляже. Сервер рахує бій до кінця одним викликом, клієнт лише програє лог.
    ///
    /// Удари тику рахуються від стану на його початку й застосовуються разом: інакше дзеркальний бій
    /// вигравала б сторона, яку обробляють першою. Бійці йдуть сталим порядком індексів, а крит і блок
    /// кидає один DeterministicRandom, тож той самий сід із тим самим загоном дає той самий бій.
    /// </summary>
    public sealed class RealtimeBattleEngine
    {
        /// <summary>Похибка накопичувача: 0.1 × 10 у double дає 0.999…, і удар запізнився б на тик.</summary>
        private const double Epsilon = 1e-9;

        private readonly RealtimeBattleConfig _config;

        public RealtimeBattleEngine(RealtimeBattleConfig config)
        {
            _config = config;
        }

        public BattleResult Run(BattleSetup setup, int seed)
        {
            var fighters = Arrange(setup);
            var random = new DeterministicRandom(seed);
            var events = new List<BattleEvent>();
            var hits = new List<(Fighter Source, Fighter Target, double Damage, HitFlags Flags)>();

            var seconds = _config.TickMilliseconds / 1000.0;
            var maxTicks = _config.MaxBattleSeconds * 1000 / _config.TickMilliseconds;
            BattleSide? winner = null;
            var tick = 0;

            while (winner is null && tick < maxTicks)
            {
                tick++;
                hits.Clear();

                foreach (var fighter in fighters)
                {
                    if (fighter.Alive && Swings(fighter, tick * seconds, seconds) && Target(fighter, fighters) is { } target)
                        hits.Add(Strike(fighter, target, random));
                }

                var warded = tick * seconds <= _config.WardSeconds + Epsilon;

                foreach (var (source, target, damage, flags) in hits)
                {
                    // Щит оберегу поглинає першим, поки не вичерпається чи не мине його час
                    var absorbed = warded ? Math.Min(target.Shield, damage) : 0;
                    target.Shield -= absorbed;
                    target.Health -= damage - absorbed;
                    events.Add(new BattleEvent(tick, BattleEventKind.Hit, source.Index, target.Index, damage, flags));
                }

                Heal(hits, tick, events);

                Bury(fighters, tick, events);
                winner = Winner(fighters);
            }

            return new BattleResult(
                winner ?? BattleSide.Defender,
                tick,
                fighters.Select(f => new FighterInfo(f.Index, f.Side, f.Lane, f.Leader, f.UnitType, f.MaxHealth, f.Ward)).ToList(),
                fighters.Select(f => Math.Max(0, f.Health)).ToList(),
                events);
        }

        /// <summary>
        /// Чи б'є боєць у цьому тику. Перший удар — щойно дійшов до ворога, далі — кожні AttackInterval секунд;
        /// залишок накопичувача переходить у наступний удар, тож +5% швидкості — рівно +5% ударів.
        /// </summary>
        private static bool Swings(Fighter fighter, double time, double seconds)
        {
            if (time + Epsilon < fighter.Approach)
                return false;

            if (!fighter.Engaged)
            {
                fighter.Engaged = true;
                fighter.Progress = 1;
            }
            else
                fighter.Progress += seconds / fighter.Stats.AttackInterval;

            if (fighter.Progress + Epsilon < 1)
                return false;

            fighter.Progress = Math.Max(0, fighter.Progress - 1);

            return true;
        }

        /// <summary>
        /// Ціль липка: боєць б'є ту саму, поки вона жива. Нову шукає на найближчій доріжці; ближній бій
        /// дістає лише фронт, поки там хтось стоїть. У загоні спершу конвої, найслабший першим, — вони
        /// прикривають героя; лише герой-лучник цілить героя першим.
        /// </summary>
        private static Fighter? Target(Fighter fighter, List<Fighter> fighters)
        {
            if (fighter.Target is { Alive: true } current)
                return current;

            var enemies = fighters.Where(f => f.Alive && f.Side != fighter.Side).ToList();

            if (fighter.Melee && enemies.Any(f => f.FrontLine))
                enemies = enemies.Where(f => f.FrontLine).ToList();

            if (enemies.Count == 0)
                return null;

            var lane = enemies.OrderBy(f => Math.Abs(f.Lane - fighter.Lane)).ThenBy(f => f.Lane).First().Lane;
            var squad = enemies.Where(f => f.Lane == lane).ToList();
            var sniper = fighter.Leader is null && !fighter.Melee;

            fighter.Target = squad
                .OrderBy(f => sniper ? f.Leader is not null : f.Leader is null)
                .ThenBy(f => f.Health)
                .ThenBy(f => f.Index)
                .First();

            return fighter.Target;
        }

        /// <summary>
        /// Удар: захист гасить частку A / (A + D / DefensePerHalfDamage) від атаки — удвічі більший захист
        /// пропускає половину. Перевага типів множить уже результат.
        /// </summary>
        private (Fighter, Fighter, double, HitFlags) Strike(Fighter source, Fighter target, DeterministicRandom random)
        {
            var attack = source.Stats.Attack;
            var defense = Math.Max(0, target.Stats.Defense);
            var share = attack <= 0 ? 0 : attack / (attack + defense / _config.DefensePerHalfDamage);
            var (advantage, flags) = Advantage(source.UnitType, target.UnitType);
            var damage = attack * share * advantage;

            // Крит і блок множать уже пробиту частку (рішення 18.10.2026): крит — завжди рівно свій множник.
            // Кидок — лише в того, хто має шанс, інакше конвої з'їдали б потік і зсували всі кидки героїв
            if (source.Stats.CritChance > 0 && random.NextDouble() < source.Stats.CritChance)
            {
                damage *= source.Stats.CritMultiplier;
                flags |= HitFlags.Crit;
            }

            if (target.Stats.BlockChance > 0 && random.NextDouble() < target.Stats.BlockChance)
            {
                damage *= 1 - target.Stats.BlockPower;
                flags |= HitFlags.Blocked;
            }

            return (source, target, damage * (1 - target.Stats.DamageReduction), flags);
        }

        /// <summary>
        /// Вампіризм лікує часткою завданої шкоди — після всіх ударів тику, тож лікування не рятує того,
        /// кого в цьому ж тику вже поклали, і не залежить від порядку ударів.
        /// </summary>
        private static void Heal(List<(Fighter Source, Fighter Target, double Damage, HitFlags Flags)> hits, int tick, List<BattleEvent> events)
        {
            foreach (var (source, _, damage, _) in hits)
            {
                if (source.Stats.Lifesteal <= 0 || source.Health <= 0)
                    continue;

                var healed = Math.Min(source.MaxHealth - source.Health, damage * source.Stats.Lifesteal);

                if (healed <= 0)
                    continue;

                source.Health += healed;
                events.Add(new BattleEvent(tick, BattleEventKind.Heal, source.Index, source.Index, healed));
            }
        }

        private (double Multiplier, HitFlags Flags) Advantage(string? attacker, string? defender)
        {
            if (attacker is null || defender is null)
                return (1, HitFlags.None);

            if (_config.TypeAdvantages.GetValueOrDefault(attacker) == defender)
                return (_config.TypeAdvantageMultiplier, HitFlags.Advantage);

            if (_config.TypeAdvantages.GetValueOrDefault(defender) == attacker)
                return (1 / _config.TypeAdvantageMultiplier, HitFlags.Disadvantage);

            return (1, HitFlags.None);
        }

        /// <summary>Прибирає полеглих тику; конвої загиблого героя б'ються далі, але без його бонусів.</summary>
        private static void Bury(List<Fighter> fighters, int tick, List<BattleEvent> events)
        {
            var fallen = fighters.Where(f => f.Alive && f.Health <= 0).ToList();

            foreach (var fighter in fallen)
            {
                fighter.Alive = false;
                events.Add(new BattleEvent(tick, BattleEventKind.Death, fighter.Index, fighter.Index, 0));
            }

            foreach (var convoy in fighters.Where(f => f.Leaderless is not null && fallen.Any(h => h.Index == f.Leader)))
                convoy.Stats = convoy.Leaderless!;
        }

        /// <summary>Переможець — хто лишився на полі. Полягли обидва в одному тику — нападник не дотиснув.</summary>
        private static BattleSide? Winner(List<Fighter> fighters)
        {
            var attackers = fighters.Any(f => f.Alive && f.Side == BattleSide.Attacker);
            var defenders = fighters.Any(f => f.Alive && f.Side == BattleSide.Defender);

            return (attackers, defenders) switch
            {
                (true, false) => BattleSide.Attacker,
                (false, _) => BattleSide.Defender,
                _ => null
            };
        }

        /// <summary>Розставляє бійців: спершу нападники, кожен герой і за ним його конвої.</summary>
        private List<Fighter> Arrange(BattleSetup setup)
        {
            var fighters = new List<Fighter>();

            Place(setup.Attackers, BattleSide.Attacker, fighters);
            Place(setup.Defenders, BattleSide.Defender, fighters);

            return fighters;
        }

        private void Place(IReadOnlyList<SquadMember> squad, BattleSide side, List<Fighter> fighters)
        {
            if (squad.Count > _config.SquadSize)
                throw new ArgumentException($"A squad takes at most {_config.SquadSize} heroes, {side} has {squad.Count}.");

            if (squad.Any(m => m.Lane < 0 || m.Lane >= _config.SquadSize) || squad.Select(m => m.Lane).Distinct().Count() != squad.Count)
                throw new ArgumentException($"{side} heroes must stand on distinct lanes 0…{_config.SquadSize - 1}.");

            foreach (var member in squad)
            {
                var front = _config.FrontLineClasses.Contains(member.HeroClass);
                var approach = front ? _config.MeleeApproachSeconds : _config.RangedApproachSeconds;
                var hero = new Fighter(fighters.Count, side, member.Lane, null, member.UnitType, front, approach, member.Hero, null);

                fighters.Add(hero);

                foreach (var convoy in member.Convoys)
                    fighters.Add(new Fighter(fighters.Count, side, member.Lane, hero.Index, convoy.UnitType, front, approach, convoy.Led, convoy.Leaderless));
            }
        }

        /// <summary>Стан бійця під час бою. Конвой стоїть і б'є так само, як його герой.</summary>
        private sealed class Fighter(int index, BattleSide side, int lane, int? leader, string? unitType, bool frontLine,
            double approach, FighterStats stats, FighterStats? leaderless)
        {
            public int Index { get; } = index;
            public BattleSide Side { get; } = side;
            public int Lane { get; } = lane;
            public int? Leader { get; } = leader;
            public string? UnitType { get; } = unitType;
            public bool FrontLine { get; } = frontLine;
            public bool Melee => FrontLine;
            public double Approach { get; } = approach;
            public double MaxHealth { get; } = stats.MaxHealth;
            public double Ward { get; } = stats.MaxHealth * stats.Ward;
            public FighterStats? Leaderless { get; } = leaderless;

            public FighterStats Stats { get; set; } = stats;
            public double Health { get; set; } = stats.MaxHealth;
            public double Shield { get; set; } = stats.MaxHealth * stats.Ward;
            public bool Alive { get; set; } = true;
            public bool Engaged { get; set; }
            public double Progress { get; set; }
            public Fighter? Target { get; set; }
        }
    }
}
