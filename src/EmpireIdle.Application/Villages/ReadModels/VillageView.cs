namespace EmpireIdle.Application.Villages.ReadModels
{
    /// <summary>
    /// Село в поданні для клієнта. X/Y — клітина на мапі світу: карта центрується на ній.
    /// ShieldUntil — щит після падіння міста, лише поки він діє.
    /// </summary>
    public record VillageView(
        Guid Id,
        string Name,
        int X,
        int Y,
        List<BuildingView> Buildings,
        List<ResourceView> Resources,
        DateTime? ShieldUntil,
        VillageDamageView? Damage);

    /// <summary>
    /// Наслідки програних оборон (GDD §2.6), лише поки є пошкоджені будівлі.
    /// DefeatsToEvict — яка поразка поспіль виселить; RepairCost — ціна миттєвого ремонту всього.
    /// </summary>
    public record VillageDamageView(int DefeatStreak, int DefeatsToEvict, List<RepairCostView> RepairCost);

    public record RepairCostView(string Resource, int Amount);

    /// <summary>
    /// Будівля з порахованим буфером. StoredAmount — величина на момент запиту,
    /// вона залежить від часу й буста, тому рахується тут, а не в контролері.
    /// DamageLevel/DamagedUntil — пошкодження після програної оборони, лише поки воно діє.
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
        bool IsUnlocked,
        int? SpeedUpCostGems,
        int DamageLevel = 0,
        DateTime? DamagedUntil = null);

    /// <summary>Ресурс села.</summary>
    public record ResourceView(string ResourceType, int Amount, bool IsUnlocked);
}
