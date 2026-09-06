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
        Completed = 3
    }
}
