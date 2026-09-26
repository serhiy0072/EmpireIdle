namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Атака дійшла, а села на клітинці вже немає: армія стала табором (§2.5).
    /// Власнику — сповіщення, де стоїть його військо.
    /// </summary>
    public record MarchCamped(Guid MarchId, Guid GarrisonId, int X, int Y, DateTime OccurredAt) : IDomainEvent;
}
