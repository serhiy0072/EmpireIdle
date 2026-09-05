namespace EmpireIdle.Application.Villages.ReadModels
{
    /// <summary>Село в поданні для клієнта.</summary>
    public record VillageView(
        Guid Id,
        string Name,
        List<BuildingView> Buildings,
        List<ResourceView> Resources);

    /// <summary>
    /// Будівля з порахованим буфером. StoredAmount — величина на момент запиту,
    /// вона залежить від часу й буста, тому рахується тут, а не в контролері.
    /// </summary>
    public record BuildingView(
        Guid Id,
        string Type,
        int Level,
        DateTime LastCollectedAt,
        int StoredAmount,
        int StorageCap,
        DateTime? ConstructionCompletesAt,
        bool IsUnderConstruction,
        bool IsUnlocked);

    /// <summary>Ресурс села.</summary>
    public record ResourceView(string ResourceType, int Amount);
}
