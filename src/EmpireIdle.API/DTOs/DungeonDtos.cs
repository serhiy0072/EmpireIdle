namespace EmpireIdle.API.DTOs;

/// <summary>Склад команди на забіг: до чотирьох героїв у порядку, заданому гравцем.</summary>
public record StartDungeonRunRequest(string DungeonKey, int Level, List<Guid> HeroIds);

/// <param name="ExpectedTurn">
/// TurnNumber зі стану, який бачив клієнт. Не збігся з сервером — 409 StaleTurn,
/// і хід не застосовано: так повтор після втраченої відповіді не зіграє зайвого.
/// </param>
/// <param name="Auto">Хід обирає автобій; ability й target тоді не потрібні.</param>
public record DungeonTurnRequest(int ExpectedTurn, bool Auto, string? AbilityKey = null, int? TargetIndex = null);
