using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Exceptions;
using EmpireIdle.Domain.Services;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>Дія бійця: звичайний удар або активне вміння з вибраною ціллю.</summary>
    public record BattleAction(string? AbilityKey, int? TargetIndex);

    /// <summary>Результат одного ходу: новий стан і журнал для клієнта.</summary>
    public record TurnResult(BattleState State, TurnLog Log);

    /// <summary>
    /// Рушій покрокового бою данжів. Чиста функція: той самий стан і той самий
    /// сід дають той самий хід, тож бій відтворюється з журналу й не залежить
    /// від того, хто його рахує — сервер чи тест.
    ///
    /// Окремий від CombatCalculator навмисно: там одна формула на всю армію,
    /// тут черга ходів, перезарядки та стани. Спроба звести їх до одного
    /// розрахунку зробила б обидва гіршими.
    ///
    /// Вміння героя (GDD §6.1, рішення 08.10.2026): одне активне з перезарядкою —
    /// гравець або автобій вирішує, коли бити, — і періодичні, що спрацьовують самі
    /// наприкінці ходу героя раз на кілька його ходів.
    /// </summary>
    public class BattleEngine
    {
        private readonly DungeonsConfig _config;

        public BattleEngine(DungeonsConfig config)
        {
            _config = config;
        }

        /// <summary>Чий зараз хід; null — раунд вичерпано й потрібен NextRound.</summary>
        public static int? CurrentActor(BattleState state)
            => state.Queue.FirstOrDefault(i => state.Combatants[i].IsAlive, -1) is var index && index >= 0 ? index : null;

        /// <summary>Активне вміння бійця; null — у ворогів і героїв без вмінь.</summary>
        public static CombatSkill? ActiveOf(Combatant combatant)
            => combatant.Skills.FirstOrDefault(s => s.Kind == SkillKind.Active);

        /// <summary>
        /// Дія, яку обере автобій за поточного стану. Та сама політика працює
        /// і для ворогів: інакше довелося б тримати дві різні «розумності».
        /// </summary>
        public BattleAction ChooseAuto(BattleState state, int actorIndex)
        {
            var actor = state.Combatants[actorIndex];

            // Готове вміння б'є одразу: відкладене воно не стає сильнішим, а перезарядка стоїть
            if (ActiveOf(actor) is { Ready: true } active && IsUseful(state, actor, active))
                return new BattleAction(active.Key, PickAutoTarget(state, actor, active));

            return new BattleAction(null, PickAutoTarget(state, actor, null));
        }

        /// <summary>
        /// Виконує хід. Перезарядка й ціль перевіряються тут, а не в застосунку: правило
        /// ліній — частина бою, і ні клієнт, ні автобій не мають шансу його обійти.
        /// </summary>
        public TurnResult Execute(BattleState state, int actorIndex, BattleAction action)
        {
            var ability = Resolve(state.Combatants[actorIndex], action.AbilityKey);

            EnsureLegal(state, state.Combatants[actorIndex], ability, action);

            var combatants = state.Combatants.ToList();
            var actor = combatants[actorIndex];
            var effects = new List<TurnEffect>();
            var random = new DeterministicRandom(state.Seed + state.TurnNumber * 7919);

            // Отрута спрацьовує до дії: оглушений теж її отримує
            ApplyPoison(combatants, actorIndex, effects);
            actor = combatants[actorIndex];

            var stunned = actor.Statuses.Any(s => s.Kind == BattleStatusKind.Stun);

            if (actor.IsAlive && !stunned)
            {
                if (ability is null)
                    Strike(combatants, actorIndex, action.TargetIndex, 1.0, effects, random);
                else
                    UseSkill(combatants, actorIndex, ability, action.TargetIndex, effects, random);
            }

            // Перезарядки йдуть і в оглушеного: оглушення забирає хід, а не час.
            // Періодичне ж спрацьовує лише в того, хто діє, — оглушене чекає до наступного ходу
            Recharge(state, combatants, actorIndex, ability, canAct: !stunned, effects, random);

            TickStatuses(combatants, actorIndex);

            var queue = state.Queue.Where(i => i != actorIndex).ToList();

            var next = state with
            {
                Combatants = combatants,
                Queue = queue,
                TurnNumber = state.TurnNumber + 1,
            };

            var log = new TurnLog
            {
                TurnNumber = state.TurnNumber,
                Wave = state.Wave,
                Round = state.Round,
                ActorIndex = actorIndex,
                AbilityKey = ability?.Key,
                Stunned = stunned,
                Effects = effects,
            };

            return new TurnResult(next, log);
        }

        /// <summary>Новий раунд: черга шикується за швидкістю, живі попереду мертвих.</summary>
        public static BattleState NextRound(BattleState state)
            => state with
            {
                Round = state.Round + 1,
                Queue = state.Combatants
                    .Where(c => c.IsAlive)
                    .OrderByDescending(c => c.Speed)
                    .ThenBy(c => c.Index)
                    .Select(c => c.Index)
                    .ToList(),
            };

        /// <summary>
        /// Хвиля вичерпала стелю раундів. Рахується на старті нового раунду,
        /// тож гравець отримує рівно MaxRoundsPerWave повних раундів.
        /// </summary>
        public bool IsOutOfRounds(BattleState state) => state.Round > _config.MaxRoundsPerWave;

        /// <summary>Чи можна бити саме цю ціль за правилом ліній.</summary>
        public static bool CanTarget(BattleState state, Combatant actor, CombatSkill? ability, int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= state.Combatants.Count)
                return false;

            var target = state.Combatants[targetIndex];

            if (!target.IsAlive)
                return false;

            var wantsAlly = ability is { Target: AbilityTarget.SingleAlly or AbilityTarget.Self };

            if (wantsAlly)
                return target.Side == actor.Side;

            if (target.Side == actor.Side)
                return false;

            if (ability?.IgnoresLine == true)
                return true;

            // Провокація перебиває лінію: саме для цього її й беруть
            var taunting = state.Combatants
                .Where(c => c.Side == target.Side && c.IsAlive && c.Statuses.Any(s => s.Kind == BattleStatusKind.Taunt))
                .ToList();

            if (taunting.Count > 0)
                return taunting.Any(c => c.Index == targetIndex);

            var frontAlive = state.Combatants.Any(c => c.Side == target.Side && c.IsAlive && c.Line == BattleLine.Front);

            return !frontAlive || target.Line == BattleLine.Front;
        }

        /// <summary>Активне вміння, яке назвала дія; чужий чи невідомий ключ — відмова, а не тихий звичайний удар.</summary>
        private static CombatSkill? Resolve(Combatant actor, string? key)
        {
            if (key is null)
                return null;

            return actor.Skills.FirstOrDefault(s => s.Key == key && s.Kind == SkillKind.Active)
                ?? throw new RequirementNotMetException($"Combatant {actor.Index} has no active skill '{key}'.");
        }

        /// <summary>
        /// Дія законна в стані, який бачив той, хто ходить: вміння перезаряджене, а ціль —
        /// жива й досяжна за правилом ліній. Масові вміння й «на себе» цілі не потребують.
        /// </summary>
        private static void EnsureLegal(BattleState state, Combatant actor, CombatSkill? ability, BattleAction action)
        {
            if (ability is { Ready: false })
                throw new RequirementNotMetException($"Skill '{ability.Key}' is ready in {ability.CooldownLeft} turns.");

            var needsTarget = ability is null
                || ability.Target is AbilityTarget.SingleEnemy or AbilityTarget.SingleAlly;

            if (!needsTarget)
                return;

            if (action.TargetIndex is not { } target)
                throw new RequirementNotMetException("This action needs a target.");

            if (!CanTarget(state, actor, ability, target))
                throw new RequirementNotMetException(RefusalReasons.DungeonTargetUnreachable, "That target cannot be reached right now.");
        }

        private static bool IsUseful(BattleState state, Combatant actor, CombatSkill ability)
        {
            // Лікування в повну команду й щит на вже щитованого — змарнована перезарядка
            if (ability.HealPercent > 0)
                return state.Alive(actor.Side).Any(c => c.Health < c.MaxHealth * 0.9);

            if (ability.ShieldPercent > 0)
                return state.Alive(actor.Side).Any(c => c.ShieldPoints <= 0);

            return true;
        }

        private static int? PickAutoTarget(BattleState state, Combatant actor, CombatSkill? ability)
        {
            if (ability is { Target: AbilityTarget.AllEnemies or AbilityTarget.AllAllies })
                return null;

            if (ability is { Target: AbilityTarget.Self })
                return actor.Index;

            if (ability is { Target: AbilityTarget.SingleAlly })
            {
                var hurt = state.Alive(actor.Side)
                    .OrderBy(c => c.Health / c.MaxHealth)
                    .ThenBy(c => c.Index)
                    .FirstOrDefault();

                return hurt?.Index;
            }

            var enemySide = actor.Side == BattleSide.Heroes ? BattleSide.Enemies : BattleSide.Heroes;

            // Найслабша досяжна ціль: добити пораненого вигідніше, ніж розмазати шкоду
            var target = state.Alive(enemySide)
                .Where(c => CanTarget(state, actor, ability, c.Index))
                .OrderBy(c => c.Health)
                .ThenBy(c => c.Index)
                .FirstOrDefault();

            return target?.Index;
        }

        private void Strike(List<Combatant> combatants, int actorIndex, int? targetIndex, double multiplier,
            List<TurnEffect> effects, DeterministicRandom random)
        {
            if (targetIndex is { } index)
                Strike(combatants, actorIndex, index, multiplier, effects, random);
        }

        private void UseSkill(List<Combatant> combatants, int actorIndex, CombatSkill ability,
            int? targetIndex, List<TurnEffect> effects, DeterministicRandom random)
        {
            var actor = combatants[actorIndex];
            var allySide = actor.Side;
            var enemySide = allySide == BattleSide.Heroes ? BattleSide.Enemies : BattleSide.Heroes;

            var targets = ability.Target switch
            {
                AbilityTarget.AllEnemies => combatants.Where(c => c.Side == enemySide && c.IsAlive).Select(c => c.Index).ToList(),
                AbilityTarget.AllAllies => combatants.Where(c => c.Side == allySide && c.IsAlive).Select(c => c.Index).ToList(),
                AbilityTarget.Self => [actorIndex],
                _ => targetIndex is { } single ? [single] : [],
            };

            foreach (var target in targets)
            {
                if (ability.DamageMultiplier > 0)
                    Strike(combatants, actorIndex, target, ability.DamageMultiplier, effects, random, ability.Status, ability.StatusMagnitude, ability.StatusTurns);
                else
                    Support(combatants, actorIndex, target, ability, effects);
            }
        }

        /// <summary>
        /// Кінець ходу для вмінь: використане активне йде на повну перезарядку, решта чекають
        /// на хід менше, а періодичне, що дочекалось, спрацьовує й теж іде на перезарядку.
        /// Відлік — у власних ходах героя: швидший герой і б'є вміннями частіше.
        /// </summary>
        private void Recharge(BattleState state, List<Combatant> combatants, int actorIndex, CombatSkill? used,
            bool canAct, List<TurnEffect> effects, DeterministicRandom random)
        {
            if (combatants[actorIndex].Skills.Count == 0)
                return;

            var skills = new List<CombatSkill>(combatants[actorIndex].Skills.Count);

            foreach (var skill in combatants[actorIndex].Skills)
            {
                if (skill.Kind == SkillKind.Active)
                {
                    skills.Add(skill.Key == used?.Key
                        ? skill with { CooldownLeft = FullCooldown(skill, combatants[actorIndex]) }
                        : Tick(skill));
                    continue;
                }

                // Мертвий герой не б'є; оглушений чекає з готовим умінням до наступного ходу
                if (skill.Ready && canAct && combatants[actorIndex].IsAlive)
                {
                    FirePeriodic(state, combatants, actorIndex, skill, effects, random);
                    skills.Add(skill with { CooldownLeft = FullCooldown(skill, combatants[actorIndex]) });
                    continue;
                }

                skills.Add(Tick(skill));
            }

            combatants[actorIndex] = combatants[actorIndex] with { Skills = skills };
        }

        private static CombatSkill Tick(CombatSkill skill) => skill with { CooldownLeft = Math.Max(0, skill.CooldownLeft - 1) };

        /// <summary>
        /// Повна перезарядка після спрацювання: з перезарядкою N уміння знову готове через N ходів героя.
        /// Артефакт на перезарядку скорочує лише активне — періодичне й так б'є без участі гравця.
        /// </summary>
        private static int FullCooldown(CombatSkill skill, Combatant actor)
        {
            var reduction = skill.Kind == SkillKind.Active
                ? (int)Math.Round(actor.UniqueStats.GetValueOrDefault(DungeonStat.CooldownReduction))
                : 0;

            return Math.Max(0, skill.Cooldown - 1 - reduction);
        }

        /// <summary>
        /// Періодичне вміння б'є тією ж логікою, що й активне, але ціль обирає саме — як автобій.
        /// Наслідки позначаються ключем вміння, щоб клієнт показав, звідки вони взялися.
        /// </summary>
        private void FirePeriodic(BattleState state, List<Combatant> combatants, int actorIndex, CombatSkill skill,
            List<TurnEffect> effects, DeterministicRandom random)
        {
            var current = state with { Combatants = combatants };
            var target = PickAutoTarget(current, combatants[actorIndex], skill);

            if ((skill.Target is AbilityTarget.SingleEnemy or AbilityTarget.SingleAlly) && target is null)
                return;

            var from = effects.Count;

            UseSkill(combatants, actorIndex, skill, target, effects, random);

            for (var i = from; i < effects.Count; i++)
                effects[i] = effects[i] with { SkillKey = skill.Key };
        }

        private void Strike(List<Combatant> combatants, int actorIndex, int targetIndex, double multiplier,
            List<TurnEffect> effects, DeterministicRandom random,
            BattleStatusKind? status = null, double statusMagnitude = 0, int statusTurns = 0)
        {
            var actor = combatants[actorIndex];
            var target = combatants[targetIndex];

            if (!target.IsAlive)
                return;

            var attack = actor.Attack * (1 + Modifier(actor, BattleStatusKind.AttackUp));
            var defense = target.Defense * (1 + Modifier(target, BattleStatusKind.DefenseUp) - Modifier(target, BattleStatusKind.DefenseDown));
            defense = Math.Max(0, defense);

            var raw = attack * multiplier * Math.Max(
                _config.MinDamageShare,
                _config.DefenseSoftening / (_config.DefenseSoftening + defense));

            var critChance = _config.BaseCritChance + actor.UniqueStats.GetValueOrDefault(DungeonStat.CritChance);
            var critical = random.NextDouble() < critChance;

            if (critical)
                raw *= _config.CritMultiplier;

            raw *= 1 - Math.Min(0.8, target.UniqueStats.GetValueOrDefault(DungeonStat.DamageReduction));

            var absorbed = Math.Min(target.ShieldPoints, raw);
            var damage = Math.Round(raw - absorbed, 1);

            var health = Math.Max(0, target.Health - damage);
            var statuses = target.Statuses;

            if (status is { } kind && health > 0)
                statuses = Apply(statuses, kind, statusMagnitude, statusTurns);

            combatants[targetIndex] = target with
            {
                Health = health,
                ShieldPoints = target.ShieldPoints - absorbed,
                Statuses = statuses,
            };

            var lifesteal = actor.UniqueStats.GetValueOrDefault(DungeonStat.Lifesteal);
            var healed = 0.0;

            if (lifesteal > 0 && damage > 0)
            {
                healed = Math.Round(damage * lifesteal, 1);
                combatants[actorIndex] = combatants[actorIndex] with
                {
                    Health = Math.Min(actor.MaxHealth, combatants[actorIndex].Health + healed),
                };
            }

            effects.Add(new TurnEffect
            {
                TargetIndex = targetIndex,
                Damage = damage,
                Critical = critical,
                ShieldAbsorbed = Math.Round(absorbed, 1),
                StatusApplied = health > 0 ? status : null,
                Died = health <= 0,
            });

            if (healed > 0)
                effects.Add(new TurnEffect { TargetIndex = actorIndex, Healed = healed });
        }

        private static void Support(List<Combatant> combatants, int actorIndex, int targetIndex,
            CombatSkill ability, List<TurnEffect> effects)
        {
            var target = combatants[targetIndex];

            if (!target.IsAlive)
                return;

            var healed = ability.HealPercent > 0 ? Math.Round(target.MaxHealth * ability.HealPercent, 1) : 0;
            var shield = ability.ShieldPercent > 0 ? Math.Round(target.MaxHealth * ability.ShieldPercent, 1) : 0;
            var statuses = target.Statuses;

            if (ability.Status is { } kind)
                statuses = Apply(statuses, kind, ability.StatusMagnitude, ability.StatusTurns);

            // Щит живе рівно стільки, скільки його стан (TickStatuses) — без стану він згас би
            // наприкінці ж ходу. Повторне накладання оновлює тривалість, а очки не сумуються:
            // інакше спам щитом давав би нескінченний запас
            if (shield > 0)
                statuses = Apply(statuses, BattleStatusKind.Shield, 0, ability.ShieldTurns);

            combatants[targetIndex] = target with
            {
                Health = Math.Min(target.MaxHealth, target.Health + healed),
                ShieldPoints = Math.Max(target.ShieldPoints, shield),
                Statuses = statuses,
            };

            effects.Add(new TurnEffect
            {
                TargetIndex = targetIndex,
                Healed = healed,
                ShieldGained = shield,
                StatusApplied = ability.Status,
            });
        }

        /// <summary>Повторне накладання оновлює тривалість, а не складає ефекти.</summary>
        private static List<BattleStatus> Apply(List<BattleStatus> statuses, BattleStatusKind kind, double magnitude, int turns)
        {
            var rest = statuses.Where(s => s.Kind != kind).ToList();

            rest.Add(new BattleStatus { Kind = kind, Magnitude = magnitude, TurnsLeft = Math.Max(1, turns) });

            return rest;
        }

        private static void ApplyPoison(List<Combatant> combatants, int actorIndex, List<TurnEffect> effects)
        {
            var actor = combatants[actorIndex];
            var poison = actor.Statuses.FirstOrDefault(s => s.Kind == BattleStatusKind.Poison);

            if (poison is null || !actor.IsAlive)
                return;

            var damage = Math.Round(poison.Magnitude, 1);
            var health = Math.Max(0, actor.Health - damage);

            combatants[actorIndex] = actor with { Health = health };

            effects.Add(new TurnEffect { TargetIndex = actorIndex, Damage = damage, Died = health <= 0 });
        }

        /// <summary>Стани згасають наприкінці ходу свого носія — один хід дорівнює одному тіку.</summary>
        private static void TickStatuses(List<Combatant> combatants, int actorIndex)
        {
            var actor = combatants[actorIndex];

            var statuses = actor.Statuses
                .Select(s => s with { TurnsLeft = s.TurnsLeft - 1 })
                .Where(s => s.TurnsLeft > 0)
                .ToList();

            combatants[actorIndex] = actor with
            {
                Statuses = statuses,
                // Щит живе рівно стільки, скільки його стан
                ShieldPoints = statuses.Any(s => s.Kind == BattleStatusKind.Shield) ? actor.ShieldPoints : 0,
            };
        }

        private static double Modifier(Combatant combatant, BattleStatusKind kind)
            => combatant.Statuses.Where(s => s.Kind == kind).Sum(s => s.Magnitude);
    }
}
