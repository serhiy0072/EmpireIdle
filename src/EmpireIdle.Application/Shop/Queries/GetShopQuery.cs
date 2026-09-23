using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Shop.Queries
{
    /// <summary>Асортимент крамниці: пакети gems за гроші й предмети за gems. Спільний для всіх гравців.</summary>
    public record GetShopQuery : IRequest<ShopView>;

    public record ShopView(IReadOnlyList<GemPackView> GemPacks, IReadOnlyList<ShopItemView> Items, string Currency);

    public record GemPackView(string Key, string DisplayName, int Gems, int PriceCents, int BonusPercent);

    /// <param name="Rarity">Рядком у нижньому регістрі — як в інвентарі.</param>
    public record ShopItemView(
        string ItemKey,
        string DisplayName,
        string Description,
        string Rarity,
        string Type,
        int PriceGems,
        int MaxPerPurchase);

    internal sealed class GetShopQueryHandler : IRequestHandler<GetShopQuery, ShopView>
    {
        private readonly GameCatalog _catalog;

        public GetShopQueryHandler(GameCatalog catalog)
        {
            _catalog = catalog;
        }

        public Task<ShopView> Handle(GetShopQuery request, CancellationToken cancellationToken)
        {
            var shop = _catalog.Config.Shop;

            var packs = shop.GemPacks
                .Select(p => new GemPackView(p.Key, p.DisplayName, p.Gems, p.PriceCents, p.BonusPercent))
                .ToList();

            // Валідатор конфіга гарантує, що кожен товар є в Items
            var items = shop.Items
                .Select(offer =>
                {
                    var item = _catalog.Item(offer.ItemKey);
                    return new ShopItemView(
                        offer.ItemKey,
                        item.DisplayName,
                        item.Description,
                        item.Rarity.ToString().ToLowerInvariant(),
                        item.Type,
                        offer.PriceGems,
                        offer.MaxPerPurchase);
                })
                .ToList();

            return Task.FromResult(new ShopView(packs, items, shop.Currency));
        }
    }
}
