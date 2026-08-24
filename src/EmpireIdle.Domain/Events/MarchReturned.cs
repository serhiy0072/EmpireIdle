namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: похід завершено — армія вдома або загинула. Потрібна для перерахунку Power.</summary>
    public record MarchReturned(Guid MarchId, Guid GarrisonId, DateTime OccurredAt) : IDomainEvent;
}
