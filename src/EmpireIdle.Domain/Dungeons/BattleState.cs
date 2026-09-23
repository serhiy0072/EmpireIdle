using EmpireIdle.Domain.Enums;

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

        /// <summary>Шкала вмінь, 0..100.</summary>
        public required int Energy { get; init; }

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
}
