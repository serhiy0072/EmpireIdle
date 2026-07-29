namespace EmpireIdle.API.DTOs
{
    public record GarrisonResponse(Guid Id, Guid VillageId, List<UnitResponse> Units, List<TrainingOrderResponse> TrainingOrders);
    public record UnitResponse(string UnitType, int Count);
    public record TrainingOrderResponse(Guid Id, string UnitType, int Count, DateTime CompletesAt);

}
