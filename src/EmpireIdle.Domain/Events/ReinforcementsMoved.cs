namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Підкріплення прибуло в чуже село або було відкликане.
    ///
    /// Несе гарнізон **власника**, а не приймача: Power рахується власнику
    /// (§7.1), а рух відбувається в чужому агрегаті, тож без цієї події
    /// власникове значення оновилось би лише випадково — наступним боєм
    /// чи тренуванням.
    /// </summary>
    public record ReinforcementsMoved(Guid OwnerGarrisonId, DateTime OccurredAt) : IDomainEvent;
}
