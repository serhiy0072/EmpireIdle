namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Що армія робить на цілі. Ортогонально до TargetType: той каже,
    /// що стоїть у клітині, цей — навіщо ми туди йдемо.
    /// </summary>
    public enum MarchIntent
    {
        Attack = 1,
        Reinforce = 2
    }
}
