using EmpireIdle.Domain.Enums;
using EmpireIdle.Domain.Services.Config;

namespace EmpireIdle.Domain.Dungeons
{
    /// <summary>
    /// Стан покрокового бою — чиста структура даних без поведінки.
    /// Живе між запитами гравця, бо ручне керування означає, що між
    /// двома ходами може минути й хвилина, і перезавантаження сторінки.
    /// </summary>
    public record BattleState
    {
        /// <summary>Бійці обох сторін у сталому порядку; мертві лишаються зі здоров'ям 0.</summary>
        public required List<Combatant> Combatants { get; init; }

        /// <summary>Номер поточної хвилі, від 1.</summary>
        public required int Wave { get; init; }

        /// <summary>Скільки раундів уже пройшло в цій хвилі — проти вічних боїв.</summary>
        public required int Round { get; init; }

        /// <summary>Черга ходів у цьому раунді: індекси бійців, що ще не ходили.</summary>
        public required List<int> Queue { get; init; }

        /// <summary>Сід для всіх випадковостей бою — крит і вибір цілі авто-політикою.</summary>
        public required int Seed { get; init; }

        /// <summary>Скільки ходів уже зроблено за весь забіг: рухає сід уперед.</summary>
        public required int TurnNumber { get; init; }

        public IEnumerable<Combatant> Alive(BattleSide side)
            => Combatants.Where(c => c.Side == side && c.Health > 0);

        public bool HeroesAlive => Alive(BattleSide.Heroes).Any();

        public bool EnemiesAlive => Alive(BattleSide.Enemies).Any();
    }

    public enum BattleSide
    {
        Heroes = 0,
        Enemies = 1
    }

    /// <summary>
    /// Один боєць. Стати вже порахованi на вході в бій: рівень, тір,
    /// спорядження й сет-бонуси тут не перераховуються — інакше кожен хід
    /// тягнув би за собою інвентар.
    /// </summary>
    public record Combatant
    {
        /// <summary>Порядковий номер у бою — ним адресуються ходи й цілі.</summary>
        public required int Index { get; init; }

        public required BattleSide Side { get; init; }

        public required BattleLine Line { get; init; }

        /// <summary>Ключ героя або ворога з конфіга — для назв і артів на клієнті.</summary>
        public required string Key { get; init; }

        /// <summary>Ідентифікатор героя гравця; у ворогів порожній.</summary>
        public Guid? HeroId { get; init; }

        public required double Attack { get; init; }

        public required double Defense { get; init; }

        public required double MaxHealth { get; init; }

        public required double Health { get; init; }

        public required double Speed { get; init; }

        /// <summary>
        /// Вміння героя для бою — активне й періодичні, вже пораховані під їхній рівень.
        /// У ворогів порожньо. Не required: забіги, збережені до вмінь, читаються як «без вмінь».
        /// </summary>
        public List<CombatSkill> Skills { get; init; } = [];

        /// <summary>Скільки шкоди ще поглине щит.</summary>
        public double ShieldPoints { get; init; }

        public required List<BattleStatus> Statuses { get; init; }

        /// <summary>Унікальні властивості з артефактів; у ворогів порожньо.</summary>
        public required Dictionary<DungeonStat, double> UniqueStats { get; init; }

        public bool IsAlive => Health > 0;
    }

    /// <summary>Накладений стан із лічильником ходів.</summary>
    public record BattleStatus
    {
        public required BattleStatusKind Kind { get; init; }

        /// <summary>Шкода за хід для отрути або частка множника для бафів.</summary>
        public required double Magnitude { get; init; }

        public required int TurnsLeft { get; init; }
    }

    /// <summary>
    /// Вміння бійця, зняте на старті забігу (GDD §6.5): рівень уже вшитий у множники,
    /// тож прокачка посеред забігу бій не змінює — так само, як і стати.
    /// </summary>
    public record CombatSkill
    {
        /// <summary>Ключ вміння з конфіга героя — для назв на клієнті й вибору активного.</summary>
        public required string Key { get; init; }

        /// <summary>Active — гравець або автобій вирішує, коли; Periodic — спрацьовує саме.</summary>
        public required SkillKind Kind { get; init; }

        public required AbilityTarget Target { get; init; }

        /// <summary>Раз на скільки власних ходів героя вміння готове.</summary>
        public required int Cooldown { get; init; }

        /// <summary>Скільки власних ходів ще чекати; 0 — готове просто зараз.</summary>
        public required int CooldownLeft { get; init; }

        public double DamageMultiplier { get; init; }

        public double HealPercent { get; init; }

        public double ShieldPercent { get; init; }

        public int ShieldTurns { get; init; }

        public BattleStatusKind? Status { get; init; }

        public double StatusMagnitude { get; init; }

        public int StatusTurns { get; init; }

        public bool IgnoresLine { get; init; }

        public bool Ready => CooldownLeft <= 0;

        /// <summary>
        /// Вміння на старті бою: множник рівня вшивається в шкоду, лікування й щит, а відлік
        /// перезарядки повний — уміння з перезарядкою N уперше готове на N-му ході героя.
        /// </summary>
        public static CombatSkill Prepare(string key, SkillKind kind, SkillBattleConfig battle, double levelScale) => new()
        {
            Key = key,
            Kind = kind,
            Target = battle.Target,
            Cooldown = battle.Cooldown,
            CooldownLeft = Math.Max(0, battle.Cooldown - 1),
            DamageMultiplier = battle.DamageMultiplier * levelScale,
            HealPercent = battle.HealPercent * levelScale,
            ShieldPercent = battle.ShieldPercent * levelScale,
            ShieldTurns = battle.ShieldTurns,
            Status = battle.Status,
            StatusMagnitude = battle.StatusMagnitude,
            StatusTurns = battle.StatusTurns,
            IgnoresLine = battle.IgnoresLine,
        };
    }
}
