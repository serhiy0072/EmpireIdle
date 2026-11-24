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
