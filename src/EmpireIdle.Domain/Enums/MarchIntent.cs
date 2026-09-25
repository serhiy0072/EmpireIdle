namespace EmpireIdle.Domain.Enums
{
    /// <summary>
    /// Що армія робить на цілі. Ортогонально до TargetType: той каже,
    /// що стоїть у клітині, цей — навіщо ми туди йдемо.
    /// </summary>
    public enum MarchIntent
    {
        Attack = 1,
        Reinforce = 2,

        /// <summary>
        /// Розвідка з вежі розвідки: без героя й юнітів, швидше за військо, без бою.
        /// Ціль бачить марш і ім'я розвідника; звіт — у момент прибуття.
        /// </summary>
        Scout = 3
    }
}
