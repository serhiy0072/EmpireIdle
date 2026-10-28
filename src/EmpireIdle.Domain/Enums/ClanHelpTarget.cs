namespace EmpireIdle.Domain.Enums
{
    /// <summary>Що саме прискорює кланова допомога.</summary>
    public enum ClanHelpTarget
    {
        /// <summary>Будівництво або апгрейд будівлі.</summary>
        Construction = 0,

        /// <summary>Замовлення тренування юнітів.</summary>
        Training = 1,

        /// <summary>Дослідження в Академії (рішення 28.10.2026): свій ліміт допомог і менша частка за кожну.</summary>
        Research = 2

        // Марші свідомо відсутні: прискорювати їх означало б зламати
        // дистанцію як механіку — див. GDD §7.1
    }
}
