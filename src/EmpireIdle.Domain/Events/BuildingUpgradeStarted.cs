namespace EmpireIdle.Domain.Events
{
    /// <summary>Подія: гравець розпочав апгрейд будівлі (будівництво стартувало).</summary>
    public record BuildingUpgradeStarted(Guid VillageId, Guid PlayerId, Guid BuildingId, string BuildingType, DateTime ConstructionCompletesAt) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}
