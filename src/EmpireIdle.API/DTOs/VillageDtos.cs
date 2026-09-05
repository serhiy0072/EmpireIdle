namespace EmpireIdle.API.DTOs;

/// <summary>
/// Ручне будівництво. Ендпоінта поки немає: усі будівлі створюються
/// разом із селом, а далі лише апгрейдяться.
/// </summary>
public record AddBuildingRequest(string BuildingType);

/// <summary>Село гравця: будівлі та склад ресурсів.</summary>
public record VillageResponse(Guid Id, string Name, List<BuildingResponse> Buildings, List<ResourceResponse> Resources);

/// <summary>
/// Будівля. StoredAmount — те, що накопичилось у буфері й чекає збору;
/// понад StorageCap виробництво зупиняється.
/// </summary>
public record BuildingResponse(Guid Id, string Type, int Level, DateTime LastCollectedAt, int StoredAmount, int StorageCap, DateTime? ConstructionCompletesAt, bool IsUnderConstruction);

/// <summary>Ресурс на складі села.</summary>
public record ResourceResponse(string ResourceType, int Amount);
