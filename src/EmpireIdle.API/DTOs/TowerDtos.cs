namespace EmpireIdle.API.DTOs;

/// <summary>Наказ ручного бою: герой на доріжці Lane застосовує активне вміння на тику Tick.</summary>
public record ManualCastRequest(int Tick, int Lane);

/// <summary>Штурм поверху: номер, який бачив клієнт, і загін за доріжками.</summary>
/// <param name="Floor">Поверх, що чекає штурму; уже закритий — відмова tower.floorClosed.</param>
/// <param name="Lanes">Герої за доріжками: індекс — доріжка, null — порожня.</param>
/// <param name="Casts">Накази ручного бою від початку; null — авто, вміння спрацьовують самі.</param>
public record StormTowerFloorRequest(int Floor, List<Guid?> Lanes, List<ManualCastRequest>? Casts = null);

/// <summary>Перегляд ручного бою: той самий бій, що зарахує штурм із цими наказами, але без збереження.</summary>
public record PreviewTowerFloorRequest(int Floor, List<Guid?> Lanes, List<ManualCastRequest> Casts);
