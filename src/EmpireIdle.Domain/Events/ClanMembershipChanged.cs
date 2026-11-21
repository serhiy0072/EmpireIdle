namespace EmpireIdle.Domain.Events
{
    /// <summary>Гравець вступив у клан, вийшов чи його виключили: сила кланових технологій змінилась.</summary>
    public record ClanMembershipChanged(Guid PlayerId, Guid ClanId, DateTime OccurredAt) : IDomainEvent;
}
