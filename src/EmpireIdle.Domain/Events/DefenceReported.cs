namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія: гравцю, що оборонявся (село, контингент у споруді, табір), складено звіт.
    /// Сповіщення йде з обробника після коміту — не раніше, ніж звіт реально збережено.
    /// </summary>
    public record DefenceReported(Guid PlayerId, Guid ReportId, bool Won, string AttackerName, DateTime OccurredAt) : IDomainEvent;
}
