namespace EmpireIdle.API.DTOs;

/// <summary>Склад команди на забіг: до чотирьох героїв у порядку, заданому гравцем.</summary>
public record StartDungeonRunRequest(string DungeonKey, int Level, List<Guid> HeroIds);

/// <param name="Auto">Хід обирає автобій; ability й target тоді не потрібні.</param>
public record DungeonTurnRequest(bool Auto, string? AbilityKey = null, int? TargetIndex = null);
