using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Events
{
    /// <summary>
    /// Подія що виникає коли гравець апгрейдить будівлю.
    /// </summary>
    public record BuildingUpgraded(Guid VillageId, Guid BuildingId, string BuildingType, BuildingLevel NewLevel, string CostResource, int CostAmount) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.Now;
    }
}
