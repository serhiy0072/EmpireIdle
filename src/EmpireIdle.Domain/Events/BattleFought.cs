namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: бій завершено, звіт сформовано.</summary>
    public record BattleFought(
        Guid GarrisonId,
        Guid PlayerId,
        Guid MarchId,
        Guid ReportId,
        bool Won,
        string TargetName) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}