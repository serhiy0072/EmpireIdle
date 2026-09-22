namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Спорядження вдягли, зняли, заточили, зламали, полагодили чи прокачали.
    /// Сила гравця рахується з того, що на героях, тож кожна така зміна —
    /// привід перерахувати її.
    /// </summary>
    public record EquipmentChanged(Guid PlayerId, Guid EquipmentId, DateTime OccurredAt) : IDomainEvent;
}
