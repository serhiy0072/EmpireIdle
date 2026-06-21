
namespace EmpireIdle.API.DTOs
{
    public record VillageResponse(Guid Id, string Name, DateTime LastTickAt,
        List<BuildingResponse> Buildings, List<ResourceResponse> Resources);

    public record BuildingResponse(Guid Id, string Type, int Level, DateTime LastCollectedAt);
    public record ResourceResponse(string ResourceType, int Amount);
}
