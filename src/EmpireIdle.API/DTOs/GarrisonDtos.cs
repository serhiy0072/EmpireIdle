using EmpireIdle.Application.Garrisons.Commands;

namespace EmpireIdle.API.DTOs;

/// <summary>Замовлення тренування: тип юніта й кількість у партії.</summary>
public record TrainUnitsRequest(string UnitType, int Count);

/// <summary>Скільки юнітів кожного типу викупити.</summary>
public record RecoverUnitsRequest(Dictionary<string, int> Units);

/// <summary>
/// Лікування поранених. Payment визначає, чим платимо — ресурсами
/// чи gems: ціни й час у цих двох шляхів різні.
/// </summary>
public record HealWoundedRequest(Dictionary<string, int> Units, HealPaymentMethod Payment);

/// <summary>Гарнізон гравця: армія, поранені, черга тренувань.</summary>
public record GarrisonResponse(Guid Id, Guid VillageId, List<UnitResponse> Units, List<UnitResponse> Wounded, List<RecoverableUnitResponse> Recoverable, List<TrainingOrderResponse> TrainingOrders);

/// <summary>Юніти одного типу.</summary>
public record UnitResponse(string UnitType, int Count);

/// <summary>Партія в черзі тренування.</summary>
public record TrainingOrderResponse(Guid Id, string UnitType, int Count, DateTime CompletesAt);

/// <summary>Стек, доступний до викупу; у кожного бою свій дедлайн.</summary>
public record RecoverableUnitResponse(string UnitType, int Count, DateTime ExpiresAt, int CostGems);

