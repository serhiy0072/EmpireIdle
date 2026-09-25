using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Ворожий марш розвернувся, не дійшовши: поселення нападника переїхало.
    /// Захисники знімають тривогу одразу, а не чекають на час прибуття.
    /// </summary>
    public record HostileMarchCalledOff(
        Guid MarchId,
        MarchTargetType TargetType,
        Guid TargetId,
        DateTime OccurredAt) : IDomainEvent;
}
