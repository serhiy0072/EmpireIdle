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
/// село покинули), а армія ще повертається. TargetLevel — рівень монстра;
/// у села null. LegStartedAt — початок поточної ноги (до цілі чи додому): від нього клієнт веде загін.
/// </summary>
public record MarchResponse(
    Guid Id,
    MarchTargetType TargetType,
    Guid TargetId,
    string? TargetName,
    int? TargetLevel,
    int TargetX,
    int TargetY,
    MarchIntent Intent,
    MarchState State,
    Guid? HeroId,
    DateTime DepartedAt,
    DateTime LegStartedAt,
    DateTime ArrivesAt,
    List<MarchUnitResponse> Units,
    int SpeedUpCostGems);

/// <summary>Загін у поході.</summary>
public record MarchUnitResponse(string UnitType, int Level, int Count);

/// <summary>
/// Ворожий марш у дорозі на село гравця, соклановця чи споруду клану.
/// Від DepartedAt до ArrivesAt клієнт веде загін прямою від (FromX, FromY) до цілі.
/// </summary>
/// <param name="TargetName">Назва села або тег клану-власника споруди; null — ціль зникла.</param>
/// <param name="Mine">Ціль — село самого гравця.</param>
/// <param name="AttackerClanTag">null — нападник поза кланом.</param>
public record IncomingAttackResponse(
    Guid MarchId,
    MarchTargetType TargetType,
    Guid TargetId,
    string? TargetName,
    bool Mine,
    int TargetX,
    int TargetY,
    int FromX,
    int FromY,
    string AttackerName,
    string? AttackerClanTag,
    DateTime DepartedAt,
    DateTime ArrivesAt);
