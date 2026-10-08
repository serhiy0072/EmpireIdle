using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>
/// Товар для котирування. Заповнюється поле свого виду: EquipmentId
/// або ItemKey з Quantity.
/// </summary>
public record MarketGoodsRequest(MarketListingKind Kind, Guid? EquipmentId, string? ItemKey, int Quantity = 1);

/// <summary>Виставлення товару за фіксовану ціну в золоті.</summary>
public record ListOnMarketRequest(MarketListingKind Kind, Guid? EquipmentId, string? ItemKey, int Quantity,
    int PriceGold);

/// <summary>Створений лот.</summary>
public record MarketListingCreated(Guid ListingId);
