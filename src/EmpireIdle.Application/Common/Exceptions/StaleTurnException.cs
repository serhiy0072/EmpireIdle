namespace EmpireIdle.Application.Common.Exceptions
{
    /// <summary>
    /// Клієнт діяв зі стану, якого на сервері вже немає: відповідь на попередній
    /// хід загубилась або бій іде в іншій вкладці. Дію не застосовано —
    /// клієнт має перечитати забіг і вирішувати знову.
    ///
    /// Захист ходу замість IIdempotentRequest: хід — послідовна машина станів,
    /// і номер ходу сам є природним ключем, без рядка в таблиці на кожен хід.
    /// </summary>
    public sealed class StaleTurnException : Exception
    {
        public StaleTurnException(Guid runId, int expectedTurn, int actualTurn)
            : base($"Dungeon run {runId} is at turn {actualTurn}, not {expectedTurn}. Reload the run and act again.") { }
    }
}
