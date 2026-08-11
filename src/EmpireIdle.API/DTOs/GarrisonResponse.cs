public record GarrisonResponse(Guid Id, Guid VillageId, List<UnitResponse> Units, List<UnitResponse> Wounded, List<RecoverableUnitResponse> Recoverable, List<TrainingOrderResponse> TrainingOrders);
public record UnitResponse(string UnitType, int Count);

/// <summary>Стек, доступний до викупу; у кожного бою свій дедлайн.</summary>
public record RecoverableUnitResponse(string UnitType, int Count, DateTime ExpiresAt, int CostGems);

public record TrainingOrderResponse(Guid Id, string UnitType, int Count, DateTime CompletesAt);