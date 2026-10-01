namespace EmpireIdle.Domain.Events
{
    /// <summary>Зруйнування споруди записано в історію — учасникам клану йде лист із посиланням на запис.</summary>
    public record ClanStructureFell(Guid FallId, Guid ClanId, DateTime OccurredAt) : IDomainEvent;
}
