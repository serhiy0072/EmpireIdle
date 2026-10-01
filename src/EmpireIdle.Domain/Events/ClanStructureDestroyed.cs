namespace EmpireIdle.Domain.Events
{
    /// <summary>Кланову споруду зруйновано в бою: бонус території знято одразу.</summary>
    public record ClanStructureDestroyed(
        Guid StructureId,
        Guid ClanId,
        int X,
        int Y,
        DateTime OccurredAt) : IDomainEvent;
}
