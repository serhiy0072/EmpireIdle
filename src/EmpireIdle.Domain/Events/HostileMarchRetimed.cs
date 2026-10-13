using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Ворожий марш у дорозі прискорили — він прибуде раніше, ніж обіцяла тривога. Захисник
    /// мусить побачити новий час: інакше атака за пів хвилини лишалась би «за п'ять хвилин».
    /// </summary>
    public record HostileMarchRetimed(
        Guid MarchId,
        MarchTargetType TargetType,
        Guid TargetId,
        DateTime ArrivesAt,
        DateTime OccurredAt) : IDomainEvent;
}
