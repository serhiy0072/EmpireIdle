namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія: село впало й переселене (GDD §2.6). FallId — запис історії,
    /// на який посилається лист у скриньці виселеного.
    /// </summary>
    public record VillageFell(Guid FallId, Guid VillageId, Guid PlayerId, Guid AttackerPlayerId,
        DateTime OccurredAt) : IDomainEvent;
}
