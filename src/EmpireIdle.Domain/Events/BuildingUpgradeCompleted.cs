using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: апгрейд будівлі завершився, рівень піднято.</summary>
    public record BuildingUpgradeCompleted(Guid VillageId, Guid PlayerId, Guid BuildingId, string BuildingType, BuildingLevel NewLevel) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}