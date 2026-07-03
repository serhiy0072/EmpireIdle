using EmpireIdle.Domain.ValueObjects;

namespace EmpireIdle.Domain.Events
{
    /// <summary>Гравець зібрав накопичені ресурси з буфера будівлі.</summary>
    public record BuildingCollected(Guid VillageId, Guid PlayerId, Guid BuildingId, string ResourceType, int Amount, int NewVillageAmount) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
