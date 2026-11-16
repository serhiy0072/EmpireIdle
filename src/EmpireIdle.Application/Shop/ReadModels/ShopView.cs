using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Shop.ReadModels
{
    public record ShopView(IReadOnlyList<GemPackView> GemPacks, IReadOnlyList<ShopItemView> Items, string Currency);

    public record GemPackView(string Key, string DisplayName, int Gems, int PriceCents, int BonusPercent);

    /// <param name="Rarity">Рядком у нижньому регістрі — як в інвентарі.</param>
    /// <param name="OnSaleUntil">Кінець вікна продажу; null — пропозиція постійна.</param>
    public record ShopItemView(
        string ItemKey,
        string DisplayName,
        string Description,
        string Rarity,
        string Type,
        int PriceGems,
        int MaxPerPurchase,
        DateTime? OnSaleUntil);
}
