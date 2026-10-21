namespace EmpireIdle.API.DTOs;

/// <summary>Село гравця. X/Y — клітина на мапі світу: клієнт центрує на ній карту.</summary>
public record VillageResponse(Guid Id, string Name, int X, int Y, List<BuildingResponse> Buildings, List<ResourceResponse> Resources,
    DateTime? ShieldUntil, VillageDamageResponse? Damage, bool IsHungry, int MealPerHour);

/// <summary>
/// Наслідки програних оборон, лише поки є пошкоджені будівлі: серія поразок,
/// на якій поразці виселять, і ціна миттєвого ремонту всього.
/// </summary>
public record VillageDamageResponse(int DefeatStreak, int DefeatsToEvict, List<RepairCostResponse> RepairCost);

public record RepairCostResponse(string Resource, int Amount);

/// <summary>
/// Будівля. StoredAmount — те, що накопичилось у буфері й чекає збору;
/// понад StorageCap виробництво зупиняється.
/// </summary>
public record BuildingResponse(Guid Id, string Type, int Level, DateTime LastCollectedAt, int StoredAmount, int StorageCap, DateTime? ConstructionCompletesAt, bool IsUnderConstruction, int? SpeedUpCostGems, bool IsUnlocked,
    int DamageLevel, DateTime? DamagedUntil, double? X, double? Y);

/// <summary>Нове місце центру будівлі на плані села: вузол сітки всередині муру.</summary>
public record MoveBuildingRequest(double X, double Y);

/// <summary>Ресурс на складі села.</summary>
public record ResourceResponse(string ResourceType, long Amount, bool IsUnlocked);

/// <summary>Підсумок «зібрати все».</summary>
public record CollectAllResponse(List<CollectedResourceResponse> Collected);

/// <summary>Скільки ресурсу зараховано на склад.</summary>
public record CollectedResourceResponse(string ResourceType, int Amount);
