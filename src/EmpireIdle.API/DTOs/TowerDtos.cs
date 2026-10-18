namespace EmpireIdle.API.DTOs;

/// <summary>Штурм поверху: номер, який бачив клієнт, і загін за доріжками.</summary>
/// <param name="Floor">Поверх, що чекає штурму; уже закритий — відмова tower.floorClosed.</param>
/// <param name="Lanes">Герої за доріжками: індекс — доріжка, null — порожня.</param>
public record StormTowerFloorRequest(int Floor, List<Guid?> Lanes);
