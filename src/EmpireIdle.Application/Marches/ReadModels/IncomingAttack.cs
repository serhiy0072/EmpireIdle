using EmpireIdle.Domain.Enums;

namespace EmpireIdle.Application.Marches.ReadModels
{
    /// <summary>
    /// Ворожий марш у дорозі очима захисника: напад чи розвідка, на кого, звідки, хто веде й коли дійде.
    /// Від DepartedAt до ArrivesAt клієнт веде загін прямою від From до цілі.
    /// </summary>
    /// <param name="TargetName">Назва села або тег клану-власника споруди; null — ціль зникла.</param>
    /// <param name="TargetOwnerId">Власник села-цілі; для споруди null.</param>
    /// <param name="DefenderClanId">Клан, що захищається: власника села або власника споруди; null — захисник поза кланом.</param>
    /// <param name="AttackerClanTag">Тег клану нападника; null — нападник поза кланом.</param>
    public record IncomingAttack(
        Guid MarchId,
        MarchIntent Intent,
        MarchTargetType TargetType,
        Guid TargetId,
        string? TargetName,
        Guid? TargetOwnerId,
        Guid? DefenderClanId,
        int TargetX,
        int TargetY,
        int FromX,
        int FromY,
        Guid AttackerPlayerId,
        string AttackerName,
        string? AttackerClanTag,
        DateTime DepartedAt,
        DateTime ArrivesAt);
}
