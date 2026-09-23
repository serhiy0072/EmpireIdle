using EmpireIdle.Application.Garrisons.Commands;

namespace EmpireIdle.API.DTOs;

/// <summary>Замовлення тренування: тип юніта, рівень і кількість у партії.</summary>
public record TrainUnitsRequest(string UnitType, int Level, int Count);

/// <summary>Замовлення прокачки вже навчених юнітів на вищий рівень.</summary>
public record LevelUpUnitsRequest(string UnitType, int FromLevel, int ToLevel, int Count);

/// <summary>Скільки юнітів кожного стеку (тип@рівень) викупити.</summary>
public record RecoverUnitsRequest(Dictionary<string, int> Units);

/// <summary>
/// Лікування поранених. Payment визначає, чим платимо — ресурсами
/// чи gems: ціни й час у цих двох шляхів різні.
/// </summary>
public record HealWoundedRequest(Dictionary<string, int> Units, HealPaymentMethod Payment);

/// <summary>Гарнізон гравця: армія, поранені, черги тренувань і прокачки.</summary>
public record GarrisonResponse(
    Guid Id,
    Guid VillageId,
    List<UnitResponse> Units,
    List<UnitResponse> Wounded,
    List<RecoverableUnitResponse> Recoverable,
    List<TrainingOrderResponse> TrainingOrders,
    List<LevelUpOrderResponse> LevelUpOrders);

/// <summary>Юніти одного типу й рівня.</summary>
public record UnitResponse(string UnitType, int Level, int Count);

/// <summary>Партія в черзі тренування.</summary>
public record TrainingOrderResponse(Guid Id, string UnitType, int Level, int Count, DateTime CompletesAt, int SpeedUpCostGems);

/// <summary>Партія в черзі прокачки.</summary>
public record LevelUpOrderResponse(Guid Id, string UnitType, int FromLevel, int ToLevel, int Count, DateTime CompletesAt, int SpeedUpCostGems);

/// <summary>Стек, доступний до викупу; у кожного бою свій дедлайн.</summary>
public record RecoverableUnitResponse(string UnitType, int Level, int Count, DateTime ExpiresAt, int CostGems);
