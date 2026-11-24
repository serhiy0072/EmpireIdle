using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>
/// Запит на відправлення армії: тип цілі, її id, склад військ, до трьох героїв різних ролей і намір.
/// Намір за замовчуванням — атака: старі клієнти його не надсилають.
/// </summary>
public record SendMarchRequest(
    MarchTargetType TargetType,
    Guid TargetId,
    Dictionary<string, int> Units,
    List<Guid> HeroIds,
    MarchIntent Intent = MarchIntent.Attack);

/// <summary>Прискорення маршу: відсоток залишку дороги — один із варіантів каталогу (SpeedUp.MarchOptions).</summary>
public record SpeedUpMarchRequest(int Percent);

/// <summary>
/// Ворожий марш у дорозі на село гравця, соклановця чи споруду клану: напад (Attack) чи розвідка (Scout).
/// Від DepartedAt до ArrivesAt клієнт веде загін прямою від (FromX, FromY) до цілі.
/// </summary>
/// <param name="TargetName">Назва села або тег клану-власника споруди; null — ціль зникла.</param>
/// <param name="Mine">Ціль — село самого гравця.</param>
/// <param name="AttackerClanTag">null — нападник поза кланом.</param>
public record IncomingAttackResponse(
    Guid MarchId,
    MarchIntent Intent,
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

/// <summary>Куди відправити розвідників: чуже село чи споруда клану.</summary>
public record SendScoutRequest(MarchTargetType TargetType, Guid TargetId);
