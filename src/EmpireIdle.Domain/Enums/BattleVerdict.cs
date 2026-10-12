namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Чи можна виграти бій за межами випадковості (Combat.RandomMin…RandomMax): навіть найгірша
    /// удача нападника перемагає найкращу удачу захисника — Win; навіть найкраща програє найгіршій — Loss.
    /// </summary>
    public enum BattleVerdict
    {
        Win = 0,
        Possible = 1,
        Loss = 2
    }
}
