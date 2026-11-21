namespace EmpireIdle.Domain.Events
{
    /// <summary>Гравець вивчив рівень особистої технології; Key — «тір:вузол».</summary>
    public record ResearchCompleted(Guid PlayerId, string Key, int Level, DateTime OccurredAt) : IDomainEvent;
}
