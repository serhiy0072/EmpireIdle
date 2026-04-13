using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Events;

/// <summary>
/// Подія що виникає коли гравець збирає ресурси у своєму селі.
/// </summary>
public record ResourcesCollected(Guid VillageId, IReadOnlyDictionary<string, ResourceAmount> Resources) : IDomainEvent
{
    /// <summary>Час коли подія відбулась.</summary>
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}