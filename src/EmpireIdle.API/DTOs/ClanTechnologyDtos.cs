using EmpireIdle.Domain.Technologies;

namespace EmpireIdle.API.DTOs;

/// <summary>Підношення ресурсом у вузол на тірі; ресурс і ціну задає сам вузол.</summary>
public record OfferClanResourcesRequest(int Tier, string Node);

/// <summary>Підношення за gems у вузол на тірі за ціною, яку клієнт показав гравцеві.</summary>
public record OfferClanGemsRequest(int Tier, string Node, int ExpectedPrice);

/// <summary>Мітка керівництва на вузол на тірі; обидва поля порожні — зняти мітку.</summary>
public record MarkClanTechnologyRequest(int? Tier, string? Node);

/// <summary>Купівля за монети: вітрина, предмет, кількість і ціна, яку клієнт показав гравцеві.</summary>
public record BuyClanShopItemRequest(ClanShopKind Kind, string ItemKey, int Quantity, int ExpectedPrice);

/// <summary>Безкоштовний предмет тижневої вітрини.</summary>
public record ClaimClanFreeItemRequest(string ItemKey, int Quantity);
