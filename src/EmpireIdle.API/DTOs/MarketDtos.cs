using EmpireIdle.Domain.Enums;

namespace EmpireIdle.API.DTOs;

/// <summary>
/// Товар для котирування. Заповнюється поле свого виду: EquipmentId, HeroId
/// або ItemKey з Quantity.
/// </summary>
public record MarketGoodsRequest(MarketListingKind Kind, Guid? EquipmentId, Guid? HeroId, string? ItemKey, int Quantity = 1);

/// <summary>Виставлення товару за фіксовану ціну в золоті.</summary>
public record ListOnMarketRequest(MarketListingKind Kind, Guid? EquipmentId, Guid? HeroId, string? ItemKey, int Quantity,
    int PriceGold);

/// <summary>Створений лот.</summary>
public record MarketListingCreated(Guid ListingId);
