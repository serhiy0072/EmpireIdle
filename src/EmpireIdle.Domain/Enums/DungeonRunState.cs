namespace EmpireIdle.Domain.Enums
{
    /// <summary>Стан забігу. Перехід лише з InProgress — завершений забіг незмінний.</summary>
    public enum DungeonRunState
    {
        InProgress = 0,
        Won = 1,
        Lost = 2,

        /// <summary>Гравець вийшов сам. Енергія вже витрачена, нагороди немає.</summary>
        Abandoned = 3
    }
}
