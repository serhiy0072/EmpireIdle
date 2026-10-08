namespace EmpireIdle.Domain.Enums
{
    /// <summary>Таймер, який прискорюють gems чи предмети: у кожного своя межа (рішення 08.10.2026).</summary>
    public enum SpeedUpTimer
    {
        /// <summary>Будівництво або апгрейд будівлі.</summary>
        Construction = 0,

        /// <summary>Тренування юнітів.</summary>
        Training = 1,

        /// <summary>Підвищення рівня юнітів.</summary>
        UnitLevelUp = 2,

        /// <summary>Марш у дорозі.</summary>
        March = 3
    }
}
