using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Battles
{
    /// <summary>
    /// Одномоментний бій арени й вежі (рішення 16–18.10.2026): усі б'ють усіх за таймером атаки, вміння
    /// спрацьовують за перезарядкою, поки одна сторона не ляже. Сервер рахує бій до кінця одним викликом,
    /// клієнт лише програє лог.
    ///
    /// Дії тику рахуються від стану на його початку й застосовуються разом: інакше дзеркальний бій
    /// вигравала б сторона, яку обробляють першою. Бійці йдуть сталим порядком індексів, а крит і блок
    /// кидає один DeterministicRandom, тож той самий сід із тим самим загоном дає той самий бій.
    ///
    /// Вміння в heroes.json записані в ходах данжу — тут кожен хід стає SecondsPerTurn секунд, тож
    /// конфіг героїв спільний для обох рушіїв.
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

        private double TickSeconds => _config.TickMilliseconds / 1000.0;

        public BattleResult Run(BattleSetup setup, int seed)
        {
            var fighters = Arrange(setup);
            var random = new DeterministicRandom(seed);
            var events = new List<BattleEvent>();
            var round = new Round();

            var maxTicks = _config.MaxBattleSeconds * 1000 / _config.TickMilliseconds;
            BattleSide? winner = null;
            var tick = 0;

            while (winner is null && tick < maxTicks)
            {
                tick++;
                var time = tick * TickSeconds;
                round.Clear();

                foreach (var fighter in fighters.Where(f => f.Alive))
                    Expire(fighter, time);

                foreach (var fighter in fighters.Where(f => f.Alive))
                    Act(fighter, fighters, time, random, round);

                Resolve(round, tick, time, events);
                Bury(fighters, tick, events);
                winner = Winner(fighters);
            }

            return new BattleResult(
                winner ?? BattleSide.Defender,
                tick,
                fighters.Select(f => new FighterInfo(f.Index, f.Side, f.Lane, f.Leader, f.UnitType, f.MaxHealth, f.Ward,
                    f.Skills.Select(s => s.Skill.Key).ToList(), f.HeroKey)).ToList(),
                fighters.Select(f => Math.Max(0, f.Health)).ToList(),
                events);
        }

        /// <summary>
        /// Дії бійця в тику від стану на його початку: отрута, удар за таймером, готові вміння.
        /// Оглушений не б'є й не чаклує — готове вміння чекає кінця оглушення.
        /// </summary>
        private void Act(Fighter fighter, List<Fighter> fighters, double time, DeterministicRandom random, Round round)
        {
            if (fighter.Statuses.TryGetValue(BattleStatusKind.Poison, out var poison) && time + Epsilon >= poison.NextTick)
            {
                round.Poisons.Add((fighter, poison.Magnitude));
                fighter.Statuses[BattleStatusKind.Poison] = poison with { NextTick = poison.NextTick + 1 };
            }

            if (fighter.Statuses.ContainsKey(BattleStatusKind.Stun))
                return;

            if (Swings(fighter, time) && Target(fighter, fighters) is { } target)
                round.Hits.Add(Strike(fighter, target, random, 1));

            for (var i = 0; i < fighter.Skills.Count; i++)
            {
                if (time + Epsilon < fighter.Skills[i].NextCast)
                    continue;

                if (Cast(fighter, fighter.Skills[i].Skill, i, fighters, random, round))
                    fighter.Skills[i] = fighter.Skills[i] with { NextCast = time + fighter.Skills[i].Skill.Cooldown };
            }
        }

        /// <summary>
        /// Чи б'є боєць у цьому тику. Перший удар — щойно дійшов до ворога, далі — кожні AttackInterval секунд;
        /// залишок накопичувача переходить у наступний удар, тож +5% швидкості — рівно +5% ударів.
        /// </summary>
        private bool Swings(Fighter fighter, double time)
        {
            if (time + Epsilon < fighter.Approach)
                return false;

            if (!fighter.Engaged)
            {
                fighter.Engaged = true;
                fighter.Progress = 1;
            }
            else
                fighter.Progress += TickSeconds / fighter.Stats.AttackInterval;

            if (fighter.Progress + Epsilon < 1)
                return false;

            fighter.Progress = Math.Max(0, fighter.Progress - 1);

            return true;
        }

        /// <summary>
        /// Ціль удару липка: боєць б'є ту саму, поки вона жива. Провокація ворога перетягує героя на себе
        /// понад липкість і лінії; конвої провокації не чують.
        /// </summary>
        private static Fighter? Target(Fighter fighter, List<Fighter> fighters)
        {
            if (fighter.IsHero && Taunter(fighter, fighters) is { } taunter)
                return fighter.Target = taunter;

            if (fighter.Target is { Alive: true } current)
                return current;

            return fighter.Target = Pick(fighter, fighters, ignoreLines: !fighter.Melee);
        }

        private static Fighter? Taunter(Fighter fighter, List<Fighter> fighters)
            => fighters
                .Where(f => f.Alive && f.Side != fighter.Side && f.Statuses.ContainsKey(BattleStatusKind.Taunt))
                .OrderBy(f => Math.Abs(f.Lane - fighter.Lane))
                .ThenBy(f => f.Index)
                .FirstOrDefault();

        /// <summary>
        /// Нова ціль — на найближчій доріжці; ближній бій дістає лише найпередню живу лінію ворога:
        /// спершу воїнів, за ними лицарів, далі стрільців. У загоні спершу конвої, найслабший першим, —
        /// вони прикривають героя; лише герой-лучник цілить героя першим.
        /// </summary>
        private static Fighter? Pick(Fighter fighter, List<Fighter> fighters, bool ignoreLines)
        {
            var enemies = fighters.Where(f => f.Alive && f.Side != fighter.Side).ToList();

            if (!ignoreLines && enemies.Count > 0)
            {
                var front = enemies.Min(f => f.Line);
                enemies = enemies.Where(f => f.Line == front).ToList();
            }

            if (enemies.Count == 0)
                return null;

            var lane = enemies.OrderBy(f => Math.Abs(f.Lane - fighter.Lane)).ThenBy(f => f.Lane).First().Lane;
            var sniper = fighter.IsHero && !fighter.Melee;

            return enemies
                .Where(f => f.Lane == lane)
                .OrderBy(f => sniper ? !f.IsHero : f.IsHero)
                .ThenBy(f => f.Health)
                .ThenBy(f => f.Index)
                .First();
        }

        /// <summary>
        /// Вміння: б'є, якщо має шкоду, інакше лікує, ставить щит чи накладає стан. Масове по ворогах
        /// зачіпає загін цілі — героя й конвої — часткою AoeShare; масове по своїх — усіх героїв
        /// і власні конвої. Немає кого зачепити — вміння чекає наступного тику, а не згорає.
        /// </summary>
        private bool Cast(Fighter caster, BattleSkill skill, int index, List<Fighter> fighters, DeterministicRandom random, Round round)
        {
            var allies = fighters.Where(f => f.Alive && f.Side == caster.Side).ToList();

            var targets = skill.Target switch
            {
                AbilityTarget.SingleEnemy => Pick(caster, fighters, skill.IgnoresLine || !caster.Melee) is { } enemy ? [enemy] : [],
                AbilityTarget.AllEnemies => Pick(caster, fighters, skill.IgnoresLine || !caster.Melee) is { } primary
                    ? fighters.Where(f => f.Alive && f.Side != caster.Side && f.Lane == primary.Lane).ToList()
                    : [],
                AbilityTarget.Self => [caster],
                AbilityTarget.SingleAlly => allies.OrderBy(f => f.Health / f.MaxHealth).ThenBy(f => f.Index).Take(1).ToList(),
                _ => allies.Where(f => f.IsHero || f.Leader == caster.Index).ToList()
            };

            if (targets.Count == 0)
                return false;

            round.Casts.Add((caster, index));

            var share = skill.Target == AbilityTarget.AllEnemies ? _config.AoeShare : 1;

            foreach (var target in targets)
            {
                if (skill.DamageMultiplier > 0)
                    round.Hits.Add(Strike(caster, target, random, skill.DamageMultiplier * (1 + caster.Stats.SkillDamage) * share, skill));
                else
                    round.Supports.Add((caster, target, skill));
            }

            return true;
        }

        /// <summary>
        /// Удар: захист гасить частку A / (A + D / DefensePerHalfDamage) від атаки — удвічі більший захист
        /// пропускає половину. Множник уміння, перевага типів, крит і блок множать уже пробиту частку
        /// (рішення 18.10.2026): крит завжди рівно свій множник, а не пробиває захист додатково.
        /// </summary>
        private Hit Strike(Fighter source, Fighter target, DeterministicRandom random, double multiplier, BattleSkill? skill = null)
        {
            var attack = Attack(source);
            var defense = Math.Max(0, target.Stats.Defense * (1 + Modifier(target, BattleStatusKind.DefenseUp) - Modifier(target, BattleStatusKind.DefenseDown)));
            var share = attack <= 0 ? 0 : attack / (attack + defense / _config.DefensePerHalfDamage);
            var (advantage, flags) = Advantage(source.UnitType, target.UnitType);
            var damage = attack * share * multiplier * advantage;

            if (skill is not null)
                flags |= HitFlags.Skill;

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

            return new Hit(source, target, damage * (1 - target.Stats.DamageReduction), flags, skill, attack);
        }

        private static double Attack(Fighter fighter) => fighter.Stats.Attack * (1 + Modifier(fighter, BattleStatusKind.AttackUp));

        private static double Modifier(Fighter fighter, BattleStatusKind kind)
            => fighter.Statuses.TryGetValue(kind, out var status) ? status.Magnitude : 0;

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

        /// <summary>
        /// Наслідки тику разом: спершу вся шкода (щит поглинає першим), потім підтримка, стани й вампіризм —
        /// тож лікування не рятує того, кого в цьому ж тику вже поклали, і не залежить від порядку дій.
        /// </summary>
        private void Resolve(Round round, int tick, double time, List<BattleEvent> events)
        {
            foreach (var (caster, index) in round.Casts)
                events.Add(new BattleEvent(tick, BattleEventKind.Cast, caster.Index, caster.Index, 0, Detail: index));

            foreach (var (fighter, damage) in round.Poisons)
            {
                Damage(fighter, damage, time);
                events.Add(new BattleEvent(tick, BattleEventKind.Hit, fighter.Index, fighter.Index, damage, HitFlags.Poison, Health: Math.Max(0, fighter.Health)));
            }

            foreach (var hit in round.Hits)
            {
                Damage(hit.Target, hit.Damage, time);
                events.Add(new BattleEvent(tick, BattleEventKind.Hit, hit.Source.Index, hit.Target.Index, hit.Damage, hit.Flags, Health: Math.Max(0, hit.Target.Health)));
            }

            foreach (var hit in round.Hits)
            {
                if (hit.Skill?.Status is { } status && hit.Target.Health > 0)
                {
                    // Отрута — частка атаки на момент накладання за хід данжу, розкладена на секунди
                    var magnitude = status == BattleStatusKind.Poison
                        ? hit.Attack * hit.Skill.StatusMagnitude / _config.SecondsPerTurn
                        : hit.Skill.StatusMagnitude;

                    Afflict(hit.Target, status, magnitude, hit.Skill.StatusSeconds, time, tick, events);
                }
            }

            foreach (var (caster, target, skill) in round.Supports)
                Support(caster, target, skill, time, tick, events);

            foreach (var hit in round.Hits)
            {
                if (hit.Source.Stats.Lifesteal <= 0 || hit.Source.Health <= 0)
                    continue;

                var healed = Math.Min(hit.Source.MaxHealth - hit.Source.Health, hit.Damage * hit.Source.Stats.Lifesteal);

                if (healed <= 0)
                    continue;

                hit.Source.Health += healed;
                events.Add(new BattleEvent(tick, BattleEventKind.Heal, hit.Source.Index, hit.Source.Index, healed, Health: hit.Source.Health));
            }
        }

        /// <summary>Щит поглинає першим, поки не вичерпається чи не мине його час; решта йде в здоров'я.</summary>
        private static void Damage(Fighter target, double damage, double time)
        {
            var absorbed = time <= target.ShieldUntil + Epsilon ? Math.Min(target.Shield, damage) : 0;
            target.Shield -= absorbed;
            target.Health -= damage - absorbed;
        }

        /// <summary>
        /// Лікування, щит і стан від уміння підтримки. Щит не сумується — береться більший, а тривалість
        /// оновлюється: інакше спам щитом давав би нескінченний запас.
        /// </summary>
        private void Support(Fighter caster, Fighter target, BattleSkill skill, double time, int tick, List<BattleEvent> events)
        {
            if (target.Health <= 0)
                return;

            if (skill.HealPercent > 0 && Math.Min(target.MaxHealth - target.Health, target.MaxHealth * skill.HealPercent) is var healed and > 0)
            {
                target.Health += healed;
                events.Add(new BattleEvent(tick, BattleEventKind.Heal, caster.Index, target.Index, healed, Health: target.Health));
            }

            if (skill.ShieldPercent > 0)
            {
                var shield = target.MaxHealth * skill.ShieldPercent;
                var active = time <= target.ShieldUntil + Epsilon ? target.Shield : 0;

                target.Shield = Math.Max(active, shield);
                target.ShieldUntil = Math.Max(target.ShieldUntil, time + skill.ShieldSeconds);
                events.Add(new BattleEvent(tick, BattleEventKind.Shield, caster.Index, target.Index, target.Shield));
            }

            if (skill.Status is { } status)
                Afflict(target, status, skill.StatusMagnitude, skill.StatusSeconds, time, tick, events);
        }

        /// <summary>
        /// Накладає стан. Оглушення й провокація діють лише на героїв: конвой — це стрій, а не боєць,
        /// якого можна приголомшити. Повторне накладання оновлює тривалість, а не складає ефекти.
        /// </summary>
        private static void Afflict(Fighter target, BattleStatusKind status, double magnitude, double seconds, double time, int tick,
            List<BattleEvent> events)
        {
            if (!target.IsHero && status is BattleStatusKind.Stun or BattleStatusKind.Taunt)
                return;

            target.Statuses[status] = new Status(magnitude, time + seconds, time + 1);

            if (status == BattleStatusKind.Stun)
                target.Target = null;

            events.Add(new BattleEvent(tick, BattleEventKind.Status, target.Index, target.Index, seconds, Detail: (int)status));
        }

        /// <summary>Знімає стани, чий час минув; після оглушення герой обирає ціль наново.</summary>
        private static void Expire(Fighter fighter, double time)
        {
            foreach (var (kind, status) in fighter.Statuses.ToList())
            {
                if (time < status.Until - Epsilon)
                    continue;

                fighter.Statuses.Remove(kind);

                if (kind == BattleStatusKind.Stun)
                    fighter.Target = null;
            }
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
                var line = _config.HeroLines.GetValueOrDefault(member.HeroClass);
                var melee = !_config.RangedClasses.Contains(member.HeroClass);
                var approach = melee ? _config.MeleeApproachSeconds : _config.RangedApproachSeconds;
                var hero = new Fighter(fighters.Count, side, member.Lane, null, member.UnitType, line, melee, approach, member.Hero, null)
                {
                    ShieldUntil = _config.WardSeconds,
                    HeroKey = member.HeroKey,
                };

                foreach (var skill in (member.Skills ?? []).Where(s => s.Kind != SkillKind.Passive))
                {
                    var battleSkill = BattleSkill.From(skill, member.Hero.CooldownMultiplier, _config);
                    hero.Skills.Add(new SkillTimer(battleSkill, battleSkill.Cooldown * _config.FirstCastShare));
                }

                fighters.Add(hero);

                foreach (var convoy in member.Convoys)
                    fighters.Add(new Fighter(fighters.Count, side, member.Lane, hero.Index, convoy.UnitType, line, melee, approach, convoy.Led, convoy.Leaderless)
                    {
                        HeroKey = member.HeroKey,
                    });
            }
        }

        /// <summary>Усе, що бійці зробили в тику, — застосовується разом у Resolve.</summary>
        private sealed class Round
        {
            public List<Hit> Hits { get; } = [];
            public List<(Fighter Caster, Fighter Target, BattleSkill Skill)> Supports { get; } = [];
            public List<(Fighter Target, double Damage)> Poisons { get; } = [];
            public List<(Fighter Caster, int Skill)> Casts { get; } = [];

            public void Clear()
            {
                Hits.Clear();
                Supports.Clear();
                Poisons.Clear();
                Casts.Clear();
            }
        }

        /// <param name="Attack">Атака нападника в момент удару — від неї сила отрути.</param>
        private sealed record Hit(Fighter Source, Fighter Target, double Damage, HitFlags Flags, BattleSkill? Skill, double Attack);

        private sealed record SkillTimer(BattleSkill Skill, double NextCast);

        /// <param name="NextTick">Коли отрута вдарить наступного разу — раз на секунду.</param>
        private sealed record Status(double Magnitude, double Until, double NextTick);

        /// <summary>Стан бійця під час бою. Конвой стоїть і б'є так само, як його герой.</summary>
        private sealed class Fighter(int index, BattleSide side, int lane, int? leader, string? unitType, int line, bool melee,
            double approach, FighterStats stats, FighterStats? leaderless)
        {
            public int Index { get; } = index;
            public BattleSide Side { get; } = side;
            public int Lane { get; } = lane;
            public int? Leader { get; } = leader;
            public bool IsHero => Leader is null;
            public string? UnitType { get; } = unitType;
            public int Line { get; } = line;
            public bool Melee { get; } = melee;
            public double Approach { get; } = approach;
            public double MaxHealth { get; } = stats.MaxHealth;
            public double Ward { get; } = stats.MaxHealth * stats.Ward;
            public FighterStats? Leaderless { get; } = leaderless;
            public List<SkillTimer> Skills { get; } = [];
            public Dictionary<BattleStatusKind, Status> Statuses { get; } = [];
            public string? HeroKey { get; init; }

            public FighterStats Stats { get; set; } = stats;
            public double Health { get; set; } = stats.MaxHealth;
            public double Shield { get; set; } = stats.MaxHealth * stats.Ward;
            public double ShieldUntil { get; set; }
            public bool Alive { get; set; } = true;
            public bool Engaged { get; set; }
            public double Progress { get; set; }
            public Fighter? Target { get; set; }
        }
    }
}
