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
        Defense = 3
    }
}
