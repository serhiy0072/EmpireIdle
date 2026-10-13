namespace EmpireIdle.Domain.Enums
{
    /// <summary>На що діє активний ефект.</summary>
    public enum EffectTarget
    {
        /// <summary>Виробництво ресурсів.</summary>
        Production = 1,

        /// <summary>Сила атаки в бою.</summary>
        Attack = 2,

        /// <summary>Сила захисту в бою.</summary>
        Defense = 3,

        /// <summary>Приховує село від розвідки, поки діє. Множника не має.</summary>
        ScoutBlock = 4,

        /// <summary>Швидкість маршів гравця (пасивка звіра, GDD §5.10).</summary>
        MarchSpeed = 5,

        /// <summary>Вантажопідйомність військ гравця (пасивка звіра, GDD §5.10).</summary>
        Carry = 6
    }
}
