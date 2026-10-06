using EmpireIdle.Application.Interfaces;
using EmpireIdle.Domain.Services;
using MediatR;

namespace EmpireIdle.Application.Shop.Queries
{
    /// <summary>
    /// Асортимент крамниці: пакети gems за гроші й предмети за gems. Спільний для гравців світу;
    /// пропозиції з вікном (GDD §6.1) видно лише поки вікно відкрите.
    /// </summary>
    public record GetShopQuery : IRequest<ShopView>;

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

    internal sealed class GetShopQueryHandler : IRequestHandler<GetShopQuery, ShopView>
    {
        private readonly GameCatalog _catalog;
        private readonly IServerRepository _serverRepository;
        private readonly IServerContext _serverContext;
        private readonly TimeProvider _timeProvider;

        public GetShopQueryHandler(GameCatalog catalog, IServerRepository serverRepository, IServerContext serverContext,
            TimeProvider timeProvider)
        {
            _catalog = catalog;
            _serverRepository = serverRepository;
            _serverContext = serverContext;
            _timeProvider = timeProvider;
        }

        public async Task<ShopView> Handle(GetShopQuery request, CancellationToken cancellationToken)
        {
            var shop = _catalog.Config.Shop;
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var server = await _serverRepository.GetByIdAsync(_serverContext.ServerId, cancellationToken)
                ?? throw new InvalidOperationException($"Server {_serverContext.ServerId} not found.");

            var packs = shop.GemPacks
                .Select(p => new GemPackView(p.Key, p.DisplayName, p.Gems, p.PriceCents, p.BonusPercent))
                .ToList();

            // Валідатор конфіга гарантує, що кожен товар є в Items
            var items = shop.Items
                .Where(offer => offer.IsOnSaleAt(server.Level, server.LevelSince, now))
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
                        offer.MaxPerPurchase,
                        offer.OnSaleUntil(server.Level, server.LevelSince));
                })
                .ToList();

            return new ShopView(packs, items, shop.Currency);
        }
    }
}
