namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: партія юнітів вийшла з тренування й поповнила гарнізон.</summary>
    public record UnitsTrained(Guid GarrisonId, Guid VillageId, string UnitType, int Count, DateTime OccurredAt) : IDomainEvent;
}
