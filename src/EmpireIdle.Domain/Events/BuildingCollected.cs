namespace EmpireIdle.Domain.Events
{
    /// <summary>Гравець зібрав накопичені ресурси з буфера будівлі.</summary>
    public record BuildingCollected(
        Guid VillageId,
        Guid PlayerId,
        Guid BuildingId,
        string ResourceType,
        int Amount,
        int NewVillageAmount,
        DateTime OccurredAt) : IDomainEvent;
}
