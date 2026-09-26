namespace EmpireIdle.Domain.Enums
{
    /// <summary>Стан походу.</summary>
    public enum MarchState
    {
        /// <summary>Іде до цілі.</summary>
        Outbound = 1,

        /// <summary>Повертається додому.</summary>
        Returning = 2,

        /// <summary>Завершений (армія вдома).</summary>
        Completed = 3,

        /// <summary>
        /// Табір: атака прийшла, а села на клітинці вже немає (§2.5). Стоїть,
        /// доки власник не відкличе; сканер його не чіпає.
        /// </summary>
        Camping = 4
    }
}
