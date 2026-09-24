namespace EmpireIdle.API.DTOs;

/// <summary>
/// Ручне будівництво. Ендпоінта поки немає: усі будівлі створюються
/// разом із селом, а далі лише апгрейдяться.
/// </summary>
public record AddBuildingRequest(string BuildingType);

/// <summary>Село гравця: будівлі та склад ресурсів.</summary>
/// <summary>Село гравця. X/Y — клітина на мапі світу: клієнт центрує на ній карту.</summary>
public record VillageResponse(Guid Id, string Name, int X, int Y, List<BuildingResponse> Buildings, List<ResourceResponse> Resources,
    DateTime? ShieldUntil);

/// <summary>
/// Будівля. StoredAmount — те, що накопичилось у буфері й чекає збору;
/// понад StorageCap виробництво зупиняється.
/// </summary>
public record BuildingResponse(Guid Id, string Type, int Level, DateTime LastCollectedAt, int StoredAmount, int StorageCap, DateTime? ConstructionCompletesAt, bool IsUnderConstruction, int? SpeedUpCostGems, bool IsUnlocked);

/// <summary>Ресурс на складі села.</summary>
public record ResourceResponse(string ResourceType, int Amount, bool IsUnlocked);

/// <summary>Підсумок «зібрати все». FullStorages — ресурси, чий склад не прийняв буфер.</summary>
public record CollectAllResponse(List<CollectedResourceResponse> Collected, List<string> FullStorages);

/// <summary>Скільки ресурсу зараховано на склад.</summary>
public record CollectedResourceResponse(string ResourceType, int Amount);
