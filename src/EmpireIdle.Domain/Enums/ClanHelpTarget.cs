namespace EmpireIdle.Domain.Enums
{
    /// <summary>Що саме прискорює кланова допомога.</summary>
    public enum ClanHelpTarget
    {
        /// <summary>Будівництво або апгрейд будівлі.</summary>
        Construction = 0,

        /// <summary>Замовлення тренування юнітів.</summary>
        Training = 1

        // Марші свідомо відсутні: прискорювати їх означало б зламати
        // дистанцію як механіку — див. GDD §7.1
    }
}
