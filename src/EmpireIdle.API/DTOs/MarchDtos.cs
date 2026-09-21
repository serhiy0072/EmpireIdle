using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>
/// Запит на відправлення армії: тип цілі, її id, склад військ і намір.
/// Намір за замовчуванням — атака: старі клієнти його не надсилають.
/// </summary>
public record SendMarchRequest(
    MarchTargetType TargetType,
    Guid TargetId,
    Dictionary<string, int> Units,
    Guid HeroId,
    MarchIntent Intent = MarchIntent.Attack);

/// <summary>
/// Активний похід. TargetName null — ціль уже зникла з мапи (монстра вбили,
/// село покинули), а армія ще повертається.
/// </summary>
public record MarchResponse(
    Guid Id,
    MarchTargetType TargetType,
    Guid TargetId,
    string? TargetName,
    int TargetX,
    int TargetY,
    MarchIntent Intent,
    MarchState State,
    Guid? HeroId,
    DateTime DepartedAt,
    DateTime ArrivesAt,
    List<MarchUnitResponse> Units,
    int SpeedUpCostGems);

/// <summary>Загін у поході.</summary>
public record MarchUnitResponse(string UnitType, int Level, int Count);
