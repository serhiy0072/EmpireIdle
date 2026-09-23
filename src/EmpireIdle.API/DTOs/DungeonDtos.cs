using EmpireIdle.Application.Dungeons.Commands;
using EmpireIdle.Domain.Dungeons;
using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>Склад команди на забіг: до чотирьох героїв у порядку, заданому гравцем.</summary>
public record StartDungeonRunRequest(string DungeonKey, int Level, List<Guid> HeroIds);

/// <param name="Auto">Хід обирає автобій; ability й target тоді не потрібні.</param>
public record DungeonTurnRequest(bool Auto, string? AbilityKey = null, int? TargetIndex = null);

/// <param name="Turns">Ходи, зроблені за цей запит — клієнт програє їх анімацією.</param>
/// <param name="NextActorIndex">Чий хід далі; null — забіг завершено.</param>
public record DungeonTurnResponse(
    string State,
    List<TurnLog> Turns,
    BattleState Battle,
    int? NextActorIndex,
    DungeonRewardResponse? Reward)
{
    public static DungeonTurnResponse From(DungeonTurnResult result) => new(
        result.State.ToString(),
        result.Turns.ToList(),
        result.Battle,
        result.NextActorIndex,
        result.Reward is null
            ? null
            : new DungeonRewardResponse(
                result.Reward.Resources.Select(r => new DungeonRewardLine(r.Resource, r.Amount)).ToList(),
                result.Reward.Artifacts.ToList()));
}

public record DungeonRewardResponse(List<DungeonRewardLine> Resources, List<string> Artifacts);

public record DungeonRewardLine(string Resource, int Amount);
