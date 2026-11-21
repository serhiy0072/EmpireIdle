namespace EmpireIdle.Domain.Events
{
    /// <summary>Гравець зробив підношення клану: ресурсом чи за gems.</summary>
    public record ClanOfferingMade(Guid PlayerId, Guid ClanId, int Points, DateTime OccurredAt) : IDomainEvent;
}
