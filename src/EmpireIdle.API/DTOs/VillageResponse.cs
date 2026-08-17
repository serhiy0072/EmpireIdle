
namespace EmpireIdle.API.DTOs
{
    public record VillageResponse(Guid Id, string Name, List<BuildingResponse> Buildings, List<ResourceResponse> Resources);
    public record BuildingResponse(Guid Id, string Type, int Level, DateTime LastCollectedAt, int StoredAmount, int StorageCap, DateTime? ConstructionCompletesAt, bool IsUnderConstruction);
    public record ResourceResponse(string ResourceType, int Amount);
}
