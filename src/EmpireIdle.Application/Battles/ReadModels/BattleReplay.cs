using EmpireIdle.Domain.Battles;

namespace EmpireIdle.Application.Battles.ReadModels
{
    /// <summary>
    /// Лог одномоментного бою для відтворення на клієнті. Події йдуть колонками — масив на кожне поле, —
    /// а не об'єктом на подію: рівний бій дає десятки тисяч ударів, і назви полів у кожному роздули б
    /// відповідь у рази. Подія i — це Ticks[i], Kinds[i], Sources[i], Targets[i], Amounts[i], Flags[i], Details[i].
    /// </summary>
    /// <param name="AttackerWon">Чи переміг нападник — гравець, що натиснув «До бою».</param>
    /// <param name="TickMilliseconds">Тривалість тику: клієнт переводить Ticks у час відтворення.</param>
    public record BattleReplay(
        bool AttackerWon,
        int Ticks,
        int TickMilliseconds,
        IReadOnlyList<BattleReplayFighter> Fighters,
        IReadOnlyList<double> FinalHealth,
        BattleReplayEvents Events)
    {
        public static BattleReplay From(BattleResult result, int tickMilliseconds)
            => new(
                result.Winner == BattleSide.Attacker,
                result.Ticks,
                tickMilliseconds,
                result.Fighters.Select(f => new BattleReplayFighter(
                    f.Index, f.Side == BattleSide.Attacker, f.Lane, f.Leader, f.HeroKey, f.UnitType, Round(f.MaxHealth), Round(f.Ward), f.Skills)).ToList(),
                result.Health.Select(Round).ToList(),
                new BattleReplayEvents(
                    result.Events.Select(e => e.Tick).ToList(),
                    result.Events.Select(e => (int)e.Kind).ToList(),
                    result.Events.Select(e => e.Source).ToList(),
                    result.Events.Select(e => e.Target).ToList(),
                    result.Events.Select(e => Round(e.Amount)).ToList(),
                    result.Events.Select(e => (int)e.Flags).ToList(),
                    result.Events.Select(e => e.Detail).ToList(),
                    result.Events.Select(e => Round(e.Health)).ToList()));

        /// <summary>Десяті частки: цифри шкоди на екрані цілі, а повна точність лише роздуває лог.</summary>
        private static double Round(double value) => Math.Round(value, 1);
    }

    /// <summary>Боєць на полі.</summary>
    /// <param name="Attacker">Сторона нападника — гравця, що натиснув «До бою».</param>
    /// <param name="Leader">Індекс героя для конвою; null — це сам герой.</param>
    /// <param name="Skills">Ключі вмінь героя за номером, на який посилається подія Cast.</param>
    public record BattleReplayFighter(int Index, bool Attacker, int Lane, int? Leader, string? HeroKey, string? UnitType,
        double MaxHealth, double Ward, IReadOnlyList<string> Skills);

    /// <summary>
    /// Події бою колонками. Kinds: 0 — удар, 1 — смерть, 2 — лікування, 3 — вміння (Details — його номер у Skills),
    /// 4 — щит, 5 — стан (Details — вид стану: 0 отрута, 1 оглушення, 2 атака+, 3 захист+, 4 захист−, 5 щит, 6 провокація;
    /// Amounts — секунди). Flags — бітова маска удару: 1 перевага типу, 2 слабкість, 4 крит, 8 блок, 16 вміння, 32 отрута.
    /// Healths — здоров'я цілі після удару чи лікування: смужка ставиться на нього, а не віднімає Amounts,
    /// бо частину удару міг поглинути щит.
    /// </summary>
    public record BattleReplayEvents(
        IReadOnlyList<int> Ticks,
        IReadOnlyList<int> Kinds,
        IReadOnlyList<int> Sources,
        IReadOnlyList<int> Targets,
        IReadOnlyList<double> Amounts,
        IReadOnlyList<int> Flags,
        IReadOnlyList<int> Details,
        IReadOnlyList<double> Healths);
}
