using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.API.DTOs;

/// <summary>Підношення ресурсом: ресурс, який клієнт показав гравцеві.</summary>
public record OfferClanResourcesRequest(string Resource);

/// <summary>Підношення за gems: ціна, яку клієнт показав гравцеві.</summary>
public record OfferClanGemsRequest(int ExpectedPrice);

/// <summary>Скільки очок із запасу клану вкласти у вузол на тірі.</summary>
public record AllocateClanPointsRequest(int Tier, string Node, int Points);

/// <summary>Купівля за монети: вітрина, предмет, кількість і ціна, яку клієнт показав гравцеві.</summary>
public record BuyClanShopItemRequest(ClanShopKind Kind, string ItemKey, int Quantity, int ExpectedPrice);

/// <summary>Безкоштовний предмет тижневої вітрини.</summary>
public record ClaimClanFreeItemRequest(string ItemKey, int Quantity);
