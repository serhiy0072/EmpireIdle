using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Ворожий марш вирушив на село гравця чи кланову споруду. Захисник і його клан
    /// дізнаються одразу: тривога — це час прислати підкріплення, а не звіт після бою.
    /// Монстрів не стосується — вони не захищаються.
    /// </summary>
    public record HostileMarchLaunched(
        Guid MarchId,
        MarchTargetType TargetType,
        Guid TargetId,
        DateTime ArrivesAt,
        DateTime OccurredAt) : IDomainEvent;
}
